using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Mappings;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AccountSettingsService : IAccountSettingsService
{
    private const string WrongPasswordMessage = "Cari şifrə yanlışdır.";
    private const string InvalidCodeMessage = "Kod düzgün deyil və ya vaxtı bitib. Yenisini istəyin.";
    private const string EmailUnavailableMessage = "E-poçt göndərmək hazırda mümkün deyil. Bir az sonra yenidən cəhd edin.";

    private readonly IUserRepository _users;
    private readonly IEmailTokenRepository _tokens;
    private readonly IUserSessionRepository _sessions;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IAccountSessionService _sessionService;
    private readonly INotificationService _notifications;
    private readonly IAuditLogService _audit;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginAttemptTracker _attempts;
    private readonly IEmailSender _sender;
    private readonly IUserStateCache _stateCache;
    private readonly IValidator<ChangePasswordRequestDto> _passwordValidator;
    private readonly IValidator<RequestEmailChangeDto> _requestValidator;
    private readonly IValidator<ConfirmEmailChangeDto> _confirmValidator;
    private readonly IValidator<UpdatePhoneRequestDto> _phoneValidator;
    private readonly AuthSettings _settings;
    private readonly byte[] _codeKey;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountSettingsService> _logger;

    public AccountSettingsService(
        IUserRepository users,
        IEmailTokenRepository tokens,
        IUserSessionRepository sessions,
        INotificationPreferenceRepository preferences,
        IAccountSessionService sessionService,
        INotificationService notifications,
        IAuditLogService audit,
        IPasswordHasher passwordHasher,
        ILoginAttemptTracker attempts,
        IEmailSender sender,
        IUserStateCache stateCache,
        IValidator<ChangePasswordRequestDto> passwordValidator,
        IValidator<RequestEmailChangeDto> requestValidator,
        IValidator<ConfirmEmailChangeDto> confirmValidator,
        IValidator<UpdatePhoneRequestDto> phoneValidator,
        IOptions<AuthSettings> settings,
        IOptions<JwtSettings> jwt,
        TimeProvider timeProvider,
        ILogger<AccountSettingsService> logger)
    {
        _users = users;
        _tokens = tokens;
        _sessions = sessions;
        _preferences = preferences;
        _sessionService = sessionService;
        _notifications = notifications;
        _audit = audit;
        _passwordHasher = passwordHasher;
        _attempts = attempts;
        _sender = sender;
        _stateCache = stateCache;
        _passwordValidator = passwordValidator;
        _requestValidator = requestValidator;
        _confirmValidator = confirmValidator;
        _phoneValidator = phoneValidator;
        _settings = settings.Value;
        // The code hashes depend on the server's secret and on this purpose, so a copy of the database cannot undo a six-digit code.
        _codeKey = SHA256.HashData(Encoding.UTF8.GetBytes("aerova-change-email-code|" + jwt.Value.Secret));
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ---- overview ---------------------------------------------------------------------------------------------------------------

    public async Task<AccountOverviewDto> GetOverviewAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var now = Now();
        var open = await _sessions.GetOpenAsync(userId, now, cancellationToken);
        var lastSignIn = await _sessions.GetLatestSignInAsync(userId, cancellationToken);
        return new AccountOverviewDto(
            user.Email,
            user.EmailVerifiedAt is not null,
            user.Phone,
            user.Role.ToString(),
            user.CreatedAt,
            user.PasswordChangedAt,
            lastSignIn,
            open.Count,
            user.Role is UserRole.Admin or UserRole.SuperAdmin,
            TwoFactorAvailable: false);
    }

    // ---- password ---------------------------------------------------------------------------------------------------------------

    public async Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await _passwordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);
        await CheckPasswordAsync(user, request.CurrentPassword, cancellationToken);

        var now = Now();
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordChangedAt = now;
        EndAllSessions(user, now);
        _users.Update(user);

        // Reset codes and links that were open belong to the old password's story: they are used up.
        await CloseOpenTokensAsync(user.Id, now, cancellationToken, EmailTokenPurpose.ResetPassword, EmailTokenPurpose.ResetCode, EmailTokenPurpose.DeleteAccount);

        await _notifications.QueueAsync(user.Id, null, NotificationType.AccountSecurity, "Password changed",
            "The password of your AEROVA account was changed and you were signed out of your other devices. If this was not you, reset your password at once and contact support.",
            cancellationToken);
        await _audit.StageAsync(Actor(user), AuditActions.PasswordChanged, "User", user.Id.ToString(), null, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        _attempts.Reset(user.Email);
        _stateCache.Invalidate(user.Id);
        return await FreshSignInAsync(user, cancellationToken);
    }

    // ---- e-mail address ---------------------------------------------------------------------------------------------------------

    public async Task<EmailChangeRequestedDto> RequestEmailChangeAsync(int userId, RequestEmailChangeDto request, CancellationToken cancellationToken = default)
    {
        await _requestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);
        await CheckPasswordAsync(user, request.CurrentPassword, cancellationToken);

        var newEmail = AuthService.NormalizeEmail(request.NewEmail);
        if (newEmail == user.Email)
        {
            throw new BusinessRuleException("Bu, artıq hesabınızın e-poçt ünvanıdır.", "same_email");
        }

        if (await _users.EmailExistsAsync(newEmail, cancellationToken))
        {
            throw new ConflictException("Bu e-poçt ünvanı ilə hesab artıq mövcuddur.");
        }

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var now = Now();
        var latest = await _tokens.GetLatestCreatedAtAsync(user.Id, EmailTokenPurpose.ChangeEmail, cancellationToken);
        if (latest is not null)
        {
            var wait = latest.Value + TimeSpan.FromSeconds(Math.Max(0, _settings.EmailCooldownSeconds)) - now;
            if (wait > TimeSpan.Zero)
            {
                throw new TooManyAttemptsException("Kod bir az əvvəl göndərilib. Yenisini istəməzdən əvvəl gözləyin.", wait);
            }
        }

        if (await _tokens.CountCreatedSinceAsync(user.Id, EmailTokenPurpose.ChangeEmail, now.AddHours(-1), cancellationToken) >= Math.Max(1, _settings.ResetCodesPerHour))
        {
            throw new TooManyAttemptsException("Çox sayda kod istənilib. Bir qədər sonra yenidən cəhd edin.", TimeSpan.FromHours(1));
        }

        // An older code stops working the moment a new one is made.
        await CloseOpenTokensAsync(user.Id, now, cancellationToken, EmailTokenPurpose.ChangeEmail);
        await _tokens.DeleteExpiredAsync(now.AddDays(-7), cancellationToken);

        // Six digits can repeat; the hash column is unique, so a repeat is drawn again.
        string code, hash;
        var tries = 0;
        do
        {
            code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            hash = CodeHash(user.Id, newEmail, code);
        }
        while (await _tokens.GetByHashAsync(hash, EmailTokenPurpose.ChangeEmail, cancellationToken) is not null && ++tries < 20);

        var minutes = Math.Max(1, _settings.ResetCodeMinutes);
        var token = new EmailToken
        {
            UserId = user.Id,
            Purpose = EmailTokenPurpose.ChangeEmail,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes),
            Payload = newEmail
        };
        await _tokens.AddAsync(token, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        var (subject, text, html) = EmailTemplates.EmailChangeCode(request.Language, user.FirstName, code, minutes);
        try
        {
            await _sender.SendAsync(new EmailMessage(newEmail, subject, text, html), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // A code nobody received is closed. Nothing identifying is logged.
            token.UsedAt = Now();
            await _tokens.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", EmailTokenPurpose.ChangeEmail);
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        return new EmailChangeRequestedDto(MaskEmail(newEmail), minutes);
    }

    public async Task<AuthResponseDto> ConfirmEmailChangeAsync(int userId, ConfirmEmailChangeDto request, CancellationToken cancellationToken = default)
    {
        await _confirmValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);
        var now = Now();

        var open = (await _tokens.GetUnusedAsync(user.Id, EmailTokenPurpose.ChangeEmail, cancellationToken)).OrderByDescending(t => t.CreatedAt).FirstOrDefault();
        if (open is null || string.IsNullOrEmpty(open.Payload))
        {
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        var maxAttempts = Math.Max(1, _settings.ResetCodeMaxAttempts);
        if (open.ExpiresAt <= now || open.Attempts >= maxAttempts)
        {
            open.UsedAt = now;
            await _tokens.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        var typed = CodeHash(user.Id, open.Payload, request.Code.Trim());
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(typed), Encoding.ASCII.GetBytes(open.TokenHash)))
        {
            open.Attempts++;
            if (open.Attempts >= maxAttempts)
            {
                // The last guess is spent: this code is dead, a new one has to be asked for.
                open.UsedAt = now;
            }

            await _tokens.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        var newEmail = open.Payload;
        if (await _users.EmailExistsAsync(newEmail, cancellationToken))
        {
            // Somebody else registered the address while the code was out.
            open.UsedAt = now;
            await _tokens.SaveChangesAsync(cancellationToken);
            throw new ConflictException("Bu e-poçt ünvanı ilə hesab artıq mövcuddur.");
        }

        var oldEmail = user.Email;
        open.UsedAt = now;
        user.Email = newEmail;
        // Typing the code that went to the new address is the proof that it is the owner's.
        user.EmailVerifiedAt = now;
        EndAllSessions(user, now);
        _users.Update(user);

        // Links and codes that were sent to the old address are used up.
        await CloseOpenTokensAsync(user.Id, now, cancellationToken, EmailTokenPurpose.VerifyEmail, EmailTokenPurpose.ResetPassword, EmailTokenPurpose.ResetCode,
            EmailTokenPurpose.DeleteAccount, EmailTokenPurpose.ChangeEmail);

        await _notifications.QueueAsync(user.Id, null, NotificationType.AccountSecurity, "E-mail address changed",
            "The e-mail address of your AEROVA account was changed and you were signed out of your other devices. If this was not you, contact support at once.",
            cancellationToken);
        await _audit.StageAsync(Actor(user), AuditActions.EmailChanged, "User", user.Id.ToString(), null, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        _stateCache.Invalidate(user.Id);

        // The old address is told too (best effort), so a stolen account cannot change its address unnoticed.
        if (_sender.IsConfigured)
        {
            var (subject, text, html) = EmailTemplates.EmailChangedNotice(null, user.FirstName, MaskEmail(newEmail));
            try
            {
                await _sender.SendAsync(new EmailMessage(oldEmail, subject, text, html), cancellationToken);
            }
            catch (EmailDeliveryException)
            {
                _logger.LogWarning("The notice of an e-mail change could not be delivered.");
            }
        }

        return await FreshSignInAsync(user, cancellationToken);
    }

    // ---- phone ------------------------------------------------------------------------------------------------------------------

    public async Task<ProfileDto> UpdatePhoneAsync(int userId, UpdatePhoneRequestDto request, CancellationToken cancellationToken = default)
    {
        await _phoneValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Replace(" ", string.Empty);
        _users.Update(user);
        await _users.SaveChangesAsync(cancellationToken);
        return user.ToProfileDto();
    }

    // ---- sessions ---------------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(int userId, string? currentSessionKey, CancellationToken cancellationToken = default)
    {
        var open = await _sessions.GetOpenAsync(userId, Now(), cancellationToken);
        return open.Select(s => new SessionDto(s.Id, s.Device, s.MaskedIp, s.CreatedAt, s.ExpiresAt,
            currentSessionKey is not null && s.SessionKey == currentSessionKey)).ToList();
    }

    public async Task RevokeSessionAsync(int userId, int sessionId, string? currentSessionKey, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        // Another account's session is reported as "not found", so ids cannot be probed.
        if (session is null || session.UserId != userId || session.RevokedAt is not null)
        {
            throw new NotFoundException("Sessiya tapılmadı.");
        }

        if (currentSessionKey is not null && session.SessionKey == currentSessionKey)
        {
            throw new BusinessRuleException("Bu cihazdan çıxmaq üçün \"Çıxış\" düyməsindən istifadə edin.", "current_session");
        }

        session.RevokedAt = Now();
        _sessions.Update(session);
        await _sessions.SaveChangesAsync(cancellationToken);
        _stateCache.Invalidate(userId);
    }

    public async Task SignOutAsync(int userId, string? currentSessionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentSessionKey))
        {
            return;
        }

        var session = await _sessions.GetByKeyAsync(currentSessionKey, cancellationToken);
        if (session is null || session.UserId != userId || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = Now();
        _sessions.Update(session);
        await _sessions.SaveChangesAsync(cancellationToken);
        _stateCache.Invalidate(userId);
    }

    public async Task<AuthResponseDto> RevokeOtherSessionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var now = Now();
        EndAllSessions(user, now);
        _users.Update(user);
        await _notifications.QueueAsync(user.Id, null, NotificationType.AccountSecurity, "Signed out of other devices",
            "You signed out of all other devices of your AEROVA account. If this was not you, change your password at once and contact support.",
            cancellationToken);
        await _audit.StageAsync(Actor(user), AuditActions.SessionsRevoked, "User", user.Id.ToString(), null, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        _stateCache.Invalidate(user.Id);
        return await FreshSignInAsync(user, cancellationToken);
    }

    // ---- e-mail preferences -----------------------------------------------------------------------------------------------------

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default)
    {
        await GetUserAsync(userId, cancellationToken);
        return ToDto(await _preferences.GetByUserIdAsync(userId, cancellationToken));
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(int userId, UpdateNotificationPreferencesDto request, CancellationToken cancellationToken = default)
    {
        await GetUserAsync(userId, cancellationToken);
        var row = await _preferences.GetByUserIdAsync(userId, cancellationToken);
        if (row is null)
        {
            row = new NotificationPreference { UserId = userId };
            await _preferences.AddAsync(row, cancellationToken);
        }
        else
        {
            row.UpdatedAt = Now();
        }

        row.FlightUpdates = request.FlightUpdates;
        row.SystemMessages = request.SystemMessages;
        row.Marketing = request.Marketing;
        await _preferences.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------------------

    private static NotificationPreferencesDto ToDto(NotificationPreference? row)
        => new(true, true, true, row?.FlightUpdates ?? true, row?.SystemMessages ?? true, row?.Marketing ?? true);

    private async Task<User> GetUserAsync(int userId, CancellationToken cancellationToken)
        => await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("İstifadəçi tapılmadı.");

    /// <summary>Checks the current password with the same pause as a sign-in: guessing it here is guessing it there.</summary>
    private async Task CheckPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        if (_attempts.IsLockedOut(user.Email, out var retryAfter))
        {
            throw new TooManyAttemptsException($"Çox sayda uğursuz cəhd. {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes))} dəqiqədən sonra yenidən cəhd edin.", retryAfter);
        }

        if (string.IsNullOrEmpty(password) || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            _attempts.RecordFailure(user.Email);
            await _audit.RecordAsync(Actor(user), AuditActions.PasswordCheckRefused, "User", user.Id.ToString(), "Wrong current password", cancellationToken);
            throw new BusinessRuleException(WrongPasswordMessage, "wrong_password");
        }
    }

    /// <summary>Ends every token issued so far (the cut-off) and closes the open sessions; the caller makes a fresh sign-in for this device.</summary>
    private void EndAllSessions(User user, DateTime now)
    {
        // JWT times have a precision of a second: the cut-off is a whole second, so the fresh token made right after it is never older than it.
        user.SessionsValidFrom = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    private async Task<AuthResponseDto> FreshSignInAsync(User user, CancellationToken cancellationToken)
    {
        // The sessions of the old tokens are closed in one go; the cut-off already refuses their tokens.
        foreach (var session in await _sessions.GetOpenAsync(user.Id, Now(), cancellationToken))
        {
            session.RevokedAt = Now();
        }

        await _sessions.SaveChangesAsync(cancellationToken);
        _stateCache.Invalidate(user.Id);
        var (token, expiresAt) = await _sessionService.IssueAsync(user, cancellationToken);
        return new AuthResponseDto(token, expiresAt, user.ToUserDto());
    }

    private async Task CloseOpenTokensAsync(int userId, DateTime now, CancellationToken cancellationToken, params EmailTokenPurpose[] purposes)
    {
        foreach (var purpose in purposes)
        {
            foreach (var open in await _tokens.GetUnusedAsync(userId, purpose, cancellationToken))
            {
                open.UsedAt = now;
            }
        }
    }

    private static AuditActor Actor(User user) => new(user.Id, user.Role.ToString());

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>"a***@gmail.com": enough to recognise the address, not enough to read it.</summary>
    internal static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return email[0] + "***" + email[at..];
    }

    /// <summary>HMAC of the code, the account and the new address: the same six digits for another account or address give another value.</summary>
    private string CodeHash(int userId, string newEmail, string code)
        => Convert.ToHexString(HMACSHA256.HashData(_codeKey, Encoding.UTF8.GetBytes($"{userId}:{newEmail}:{code}"))).ToLowerInvariant();
}
