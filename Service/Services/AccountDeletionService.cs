using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Deleting an account takes two steps and never happens without the owner's mailbox. Step one (signed in, with the password) only
/// e-mails a link. Step two opens that link's page, which asks again; only pressing the button there deletes. A mail program that opens
/// links therefore cannot delete anything. The link token is 256 random bits shown only in the e-mail; the database keeps its SHA-256
/// hash, an expiry and the moment it was used. No token, link, address or password is ever written to the log or the audit trail.
///
/// "Deleting" erases the personal data and closes the account but keeps the row: bookings, payments and refunds (which the law makes the
/// airline keep) point at it. The row gets an unusable password, no name, phone, birth date or nationality, and an address that cannot
/// receive mail, so the real address is free to register again.
/// </summary>
public class AccountDeletionService : IAccountDeletionService
{
    private const string EmailUnavailableMessage = "Hazırda e-poçt göndərə bilmirik. Bir az sonra yenidən cəhd edin.";
    private const string InvalidLinkMessage = "Bu keçid etibarsızdır.";
    private const string NotDeletableMessage = "Bu hesab növü bu yolla silinə bilməz.";
    private const string OpenTripsMessage = "Gələcək uçuşu olan, ödənişi gözləyən və ya qaytarması davam edən bronlarınız var. Əvvəl onları ləğv edin və ya tamamlanmasını gözləyin, sonra hesabı silin.";

    /// <summary>The domain of the address a deleted account keeps: ".invalid" can never receive mail.</summary>
    internal const string DeletedEmailDomain = "deleted.invalid";

    private readonly IUserRepository _users;
    private readonly IEmailTokenRepository _tokens;
    private readonly IAccountErasureRepository _erasure;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginAttemptTracker _attempts;
    private readonly IUserStateCache _stateCache;
    private readonly IAuditLogService _audit;
    private readonly IEmailSender _sender;
    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        IUserRepository users,
        IEmailTokenRepository tokens,
        IAccountErasureRepository erasure,
        IPasswordHasher passwordHasher,
        ILoginAttemptTracker attempts,
        IUserStateCache stateCache,
        IAuditLogService audit,
        IEmailSender sender,
        IOptions<AuthSettings> settings,
        TimeProvider timeProvider,
        ILogger<AccountDeletionService> logger)
    {
        _users = users;
        _tokens = tokens;
        _erasure = erasure;
        _passwordHasher = passwordHasher;
        _attempts = attempts;
        _stateCache = stateCache;
        _audit = audit;
        _sender = sender;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AccountDeletionLinkDto> RequestAsync(int userId, AccountDeletionRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("Hesab tapılmadı.");
        }

        var actor = new AuditActor(user.Id, user.Role.ToString());

        // Staff accounts are managed by a SuperAdmin; a customer link must never be able to remove one.
        if (user.Role != UserRole.User)
        {
            await _audit.RecordAsync(actor, AuditActions.AccountDeletionRefused, "User", user.Id.ToString(), "Staff account", cancellationToken);
            throw new BusinessRuleException(NotDeletableMessage, "account_not_deletable");
        }

        // The same pause as a sign-in: guessing the password here is guessing the password there.
        if (_attempts.IsLockedOut(user.Email, out var retryAfter))
        {
            throw new TooManyAttemptsException($"Çox sayda uğursuz cəhd. {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes))} dəqiqədən sonra yenidən cəhd edin.", retryAfter);
        }

        if (string.IsNullOrEmpty(request.Password) || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _attempts.RecordFailure(user.Email);
            await _audit.RecordAsync(actor, AuditActions.AccountDeletionRefused, "User", user.Id.ToString(), "Wrong password", cancellationToken);
            throw new BusinessRuleException("Şifrə yanlışdır.", "wrong_password");
        }

        var now = Now();
        if (await _erasure.HasOpenTripsAsync(user.Id, now, cancellationToken))
        {
            await _audit.RecordAsync(actor, AuditActions.AccountDeletionRefused, "User", user.Id.ToString(), "Open bookings", cancellationToken);
            throw new BusinessRuleException(OpenTripsMessage, "open_bookings");
        }

        // Without a mailbox to confirm in there is no deletion at all.
        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var latest = await _tokens.GetLatestCreatedAtAsync(user.Id, EmailTokenPurpose.DeleteAccount, cancellationToken);
        if (latest is not null)
        {
            var wait = latest.Value + TimeSpan.FromSeconds(Math.Max(0, _settings.EmailCooldownSeconds)) - now;
            if (wait > TimeSpan.Zero)
            {
                throw new TooManyAttemptsException("Təsdiq keçidi bir az əvvəl göndərilib. Yenisini istəməzdən əvvəl gözləyin.", wait);
            }
        }

        // A new link replaces the older ones.
        foreach (var open in await _tokens.GetUnusedAsync(user.Id, EmailTokenPurpose.DeleteAccount, cancellationToken))
        {
            open.UsedAt = now;
        }

        var minutes = Math.Max(5, _settings.DeleteAccountLinkMinutes);
        var token = NewToken();
        await _tokens.AddAsync(new EmailToken
        {
            UserId = user.Id,
            Purpose = EmailTokenPurpose.DeleteAccount,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes)
        }, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        var language = string.IsNullOrWhiteSpace(request.Language) ? null : EmailTemplates.NormalizeLanguage(request.Language);
        var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/account-delete-confirm.html?token={Uri.EscapeDataString(token)}";
        var (subject, text, html) = EmailTemplates.AccountDeletionConfirmation(language, user.FirstName, link, minutes);

        try
        {
            await _sender.SendAsync(new EmailMessage(user.Email, subject, text, html), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", EmailTokenPurpose.DeleteAccount);
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        await _audit.RecordAsync(actor, AuditActions.AccountDeletionRequested, "User", user.Id.ToString(), null, cancellationToken);
        return new AccountDeletionLinkDto(true, CancellationLinkService.Mask(user.Email), minutes);
    }

    public async Task<AccountDeletionCheckDto> CheckAsync(string token, CancellationToken cancellationToken = default)
    {
        var stored = await FindUsableAsync(token, cancellationToken);
        return new AccountDeletionCheckDto(CancellationLinkService.Mask(stored.User.Email), stored.ExpiresAt);
    }

    public async Task<AccountDeletedDto> ConfirmAsync(ConfirmAccountDeletionRequestDto request, CancellationToken cancellationToken = default)
    {
        var stored = await FindUsableAsync(request.Token, cancellationToken);
        var user = stored.User;
        var actor = new AuditActor(user.Id, user.Role.ToString());
        var now = Now();

        // Things may have changed since the link was asked for: look again before erasing anything.
        if (user.Role != UserRole.User)
        {
            await _audit.RecordAsync(actor, AuditActions.AccountDeletionRefused, "User", user.Id.ToString(), "Staff account", cancellationToken);
            throw new BusinessRuleException(NotDeletableMessage, "account_not_deletable");
        }

        if (await _erasure.HasOpenTripsAsync(user.Id, now, cancellationToken))
        {
            await _audit.RecordAsync(actor, AuditActions.AccountDeletionRefused, "User", user.Id.ToString(), "Open bookings", cancellationToken);
            throw new BusinessRuleException(OpenTripsMessage, "open_bookings");
        }

        var oldEmail = user.Email;
        var name = user.FirstName;
        var language = string.IsNullOrWhiteSpace(request.Language) ? null : EmailTemplates.NormalizeLanguage(request.Language);

        // Used up together with the erasure: both are saved in one unit of work, and a failure saves neither.
        stored.UsedAt = now;
        await _erasure.StageErasureAsync(user.Id, oldEmail, stored.Id, cancellationToken);

        user.Email = $"deleted-{user.Id}-{Guid.NewGuid():N}@{DeletedEmailDomain}";
        user.FirstName = "Silinmiş";
        user.LastName = "hesab";
        user.Title = null;
        user.DateOfBirth = null;
        user.NationalityCode = null;
        user.Phone = null;
        user.PasswordHash = _passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        user.EmailVerifiedAt = null;
        user.IsActive = false;
        _users.Update(user);

        await _audit.StageAsync(actor, AuditActions.AccountDeleted, "User", user.Id.ToString(), null, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        // Every token of this account stops working at once (the sign-in check reads the active flag).
        _stateCache.Invalidate(user.Id);
        _attempts.Reset(oldEmail);

        if (_sender.IsConfigured)
        {
            try
            {
                var (subject, text, html) = EmailTemplates.AccountDeleted(language, name);
                await _sender.SendAsync(new EmailMessage(oldEmail, subject, text, html), cancellationToken);
            }
            catch (EmailDeliveryException)
            {
                // The account is already gone; only the notice is lost.
                _logger.LogWarning("The account deleted notice could not be delivered.");
            }
        }

        return new AccountDeletedDto(true);
    }

    /// <summary>The token's record when it can still be used; otherwise the reason, as a 422 with a code.</summary>
    private async Task<EmailToken> FindUsableAsync(string? token, CancellationToken cancellationToken)
    {
        var value = (token ?? string.Empty).Trim();
        var stored = value.Length is < 20 or > 200 ? null : await _tokens.GetByHashAsync(Hash(value), EmailTokenPurpose.DeleteAccount, cancellationToken);
        if (stored is null)
        {
            throw new BusinessRuleException(InvalidLinkMessage, "link_invalid");
        }

        if (stored.UsedAt is not null)
        {
            throw new BusinessRuleException(stored.User.IsActive
                ? "Bu keçid artıq istifadə olunub. Lazım olsa, profildən yenisini istəyin."
                : "Bu keçid artıq istifadə olunub. Hesab silinib.", stored.User.IsActive ? "link_used" : "link_confirmed");
        }

        if (stored.ExpiresAt <= Now())
        {
            throw new BusinessRuleException("Bu keçidin vaxtı bitib. Profildən yenisini istəyin.", "link_expired");
        }

        if (!stored.User.IsActive)
        {
            throw new BusinessRuleException(InvalidLinkMessage, "link_invalid");
        }

        return stored;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
