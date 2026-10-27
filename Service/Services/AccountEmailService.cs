using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Links and codes sent by e-mail. A link token is 256 random bits shown only in the e-mail; the database keeps its SHA-256 hash, an
/// expiry and the moment it was used. A password reset sends a six-digit code instead: it is kept as an HMAC (a bare hash of six digits
/// could be undone at once), lives a few minutes, works once and dies after a few wrong guesses; a correct one is traded for a
/// one-time ticket that sets the new password. Neither a token, a code, a link, the address nor the SMTP login is ever written to the log.
/// </summary>
public class AccountEmailService : IAccountEmailService
{
    private const string InvalidLinkMessage = "This link is invalid or has expired.";
    private const string InvalidCodeMessage = "That code is not correct or has expired. Ask for a new one.";
    private const string NoAccountMessage = "No account was found for this email address.";
    private const string EmailUnavailableMessage = "We cannot send e-mail right now. Please try again later.";

    private readonly IUserRepository _users;
    private readonly IEmailTokenRepository _tokens;
    private readonly IEmailSender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginAttemptTracker _attempts;
    private readonly IValidator<VerifyEmailRequestDto> _verifyValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetValidator;
    private readonly IValidator<VerifyResetCodeRequestDto> _codeValidator;
    private readonly byte[] _codeKey;
    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountEmailService> _logger;

    public AccountEmailService(
        IUserRepository users,
        IEmailTokenRepository tokens,
        IEmailSender sender,
        IPasswordHasher passwordHasher,
        ILoginAttemptTracker attempts,
        IValidator<VerifyEmailRequestDto> verifyValidator,
        IValidator<ForgotPasswordRequestDto> forgotValidator,
        IValidator<ResetPasswordRequestDto> resetValidator,
        IValidator<VerifyResetCodeRequestDto> codeValidator,
        IOptions<JwtSettings> jwt,
        IOptions<AuthSettings> settings,
        TimeProvider timeProvider,
        ILogger<AccountEmailService> logger)
    {
        _users = users;
        _tokens = tokens;
        _sender = sender;
        _passwordHasher = passwordHasher;
        _attempts = attempts;
        _verifyValidator = verifyValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _codeValidator = codeValidator;
        // The key of the code hashes comes from the server's secret, so a copy of the database alone cannot undo a six-digit code.
        _codeKey = SHA256.HashData(Encoding.UTF8.GetBytes("aerova-reset-code|" + jwt.Value.Secret));
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool IsEmailConfigured => _sender.IsConfigured;

    public async Task<bool> SendVerificationAsync(User user, string? language, CancellationToken cancellationToken = default)
    {
        if (!_sender.IsConfigured)
        {
            return false;
        }

        if (await RetryAfterAsync(user.Id, EmailTokenPurpose.VerifyEmail, cancellationToken) > TimeSpan.Zero)
        {
            return false;
        }

        return await IssueAndSendAsync(user, EmailTokenPurpose.VerifyEmail, language, cancellationToken);
    }

    public async Task EmailChangedAsync(User user, string? language, CancellationToken cancellationToken = default)
    {
        // A link meant for the old address must not confirm the new one or reset the password.
        var now = Now();
        foreach (var purpose in new[] { EmailTokenPurpose.VerifyEmail, EmailTokenPurpose.ResetPassword, EmailTokenPurpose.ResetCode })
        {
            foreach (var token in await _tokens.GetUnusedAsync(user.Id, purpose, cancellationToken))
            {
                token.UsedAt = now;
            }
        }

        await _tokens.SaveChangesAsync(cancellationToken);

        if (_sender.IsConfigured)
        {
            await IssueAndSendAsync(user, EmailTokenPurpose.VerifyEmail, language, cancellationToken);
        }
    }

    public async Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        await _verifyValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = Now();
        var token = await _tokens.GetByHashAsync(Hash(request.Token.Trim()), EmailTokenPurpose.VerifyEmail, cancellationToken);
        if (token is null)
        {
            throw new BusinessRuleException("This link is not valid.", "link_invalid");
        }

        if (token.UsedAt is not null)
        {
            // Opened twice (or a mail scanner opened it first): when the address is confirmed the person only needs to sign in.
            throw token.User.EmailVerifiedAt is null
                ? new BusinessRuleException("This link was already used. Ask for a new one.", "link_used")
                : new BusinessRuleException("This link was already used. Your email address is confirmed.", "link_confirmed");
        }

        if (token.ExpiresAt <= now)
        {
            throw new BusinessRuleException("This link has expired. Ask for a new one.", "link_expired");
        }

        token.UsedAt = now;
        token.User.EmailVerifiedAt ??= now;
        // The address is confirmed once: the other open links are spent with it.
        foreach (var other in await _tokens.GetUnusedAsync(token.UserId, EmailTokenPurpose.VerifyEmail, cancellationToken))
        {
            other.UsedAt = now;
        }

        _users.Update(token.User);
        await _tokens.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(int userId, string? language, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Please sign in again.");
        }

        if (user.EmailVerifiedAt is not null)
        {
            throw new ConflictException("Your email address is already verified.");
        }

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var wait = await RetryAfterAsync(user.Id, EmailTokenPurpose.VerifyEmail, cancellationToken);
        if (wait > TimeSpan.Zero)
        {
            throw new TooManyAttemptsException("A message was sent a moment ago. Please wait before asking for another.", wait);
        }

        if (!await IssueAndSendAsync(user, EmailTokenPurpose.VerifyEmail, language, cancellationToken))
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await _forgotValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var user = await _users.GetByEmailAsync(AuthService.NormalizeEmail(request.Email), cancellationToken);

        // The person is told when no account uses the address (a disabled account counts as none), so nothing is sent to a stranger.
        // This is a choice for convenience: it shows which addresses have accounts, and the sign-up page already does the same.
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException(NoAccountMessage);
        }

        var wait = await RetryAfterAsync(user.Id, EmailTokenPurpose.ResetCode, cancellationToken);
        if (wait > TimeSpan.Zero)
        {
            throw new TooManyAttemptsException("A code was sent a moment ago. Please wait before asking for another.", wait);
        }

        // At most a few codes an hour per account: neither endless guessing nor a flood of mail to somebody's inbox.
        if (await _tokens.CountCreatedSinceAsync(user.Id, EmailTokenPurpose.ResetCode, Now().AddHours(-1), cancellationToken) >= Math.Max(1, _settings.ResetCodesPerHour))
        {
            throw new TooManyAttemptsException("Too many codes were asked for this account. Please try again in a while.", TimeSpan.FromHours(1));
        }

        // A code that did not go out is never reported as sent.
        if (!await IssueAndSendCodeAsync(user, request.Language, cancellationToken))
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }
    }

    public async Task<ResetTicketDto> VerifyResetCodeAsync(VerifyResetCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        await _codeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = Now();
        var user = await _users.GetByEmailAsync(AuthService.NormalizeEmail(request.Email), cancellationToken);
        var open = user is { IsActive: true }
            ? (await _tokens.GetUnusedAsync(user.Id, EmailTokenPurpose.ResetCode, cancellationToken)).OrderByDescending(t => t.CreatedAt).FirstOrDefault()
            : null;

        // The hash is made whether or not there is an account, so the time taken does not tell the two apart.
        var typed = CodeHash(user?.Id ?? 0, request.Code.Trim());

        if (open is null)
        {
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        if (open.ExpiresAt <= now || open.Attempts >= Math.Max(1, _settings.ResetCodeMaxAttempts))
        {
            open.UsedAt = now;
            await _tokens.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(typed), Encoding.ASCII.GetBytes(open.TokenHash)))
        {
            open.Attempts++;
            if (open.Attempts >= Math.Max(1, _settings.ResetCodeMaxAttempts))
            {
                // The last guess is spent: this code is dead, a new one has to be asked for.
                open.UsedAt = now;
            }

            await _tokens.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(InvalidCodeMessage, "code_invalid");
        }

        // The code works once. Its ticket (and nothing else) lets the new password be set, for a few minutes.
        open.UsedAt = now;
        foreach (var older in await _tokens.GetUnusedAsync(user!.Id, EmailTokenPurpose.ResetPassword, cancellationToken))
        {
            older.UsedAt = now;
        }

        var ticket = NewToken();
        var minutes = Math.Max(1, _settings.ResetTicketMinutes);
        await _tokens.AddAsync(new EmailToken
        {
            UserId = user.Id,
            Purpose = EmailTokenPurpose.ResetPassword,
            TokenHash = Hash(ticket),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes)
        }, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        return new ResetTicketDto(ticket, minutes);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await _resetValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = Now();
        var token = await _tokens.GetByHashAsync(Hash(request.Token.Trim()), EmailTokenPurpose.ResetPassword, cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= now || !token.User.IsActive)
        {
            throw new BusinessRuleException(InvalidLinkMessage, "ticket_invalid");
        }

        var user = token.User;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        // Opening the link that was sent to the address shows the person controls it.
        user.EmailVerifiedAt ??= now;
        _users.Update(user);

        // This ticket, any other reset link still open and any reset code still open are used up.
        foreach (var purpose in new[] { EmailTokenPurpose.ResetPassword, EmailTokenPurpose.ResetCode })
        {
            foreach (var open in await _tokens.GetUnusedAsync(user.Id, purpose, cancellationToken))
            {
                open.UsedAt = now;
            }
        }

        await _tokens.SaveChangesAsync(cancellationToken);

        // A person who just chose a new password should not meet a sign-in pause caused by earlier failures.
        _attempts.Reset(user.Email);
    }

    private async Task<bool> IssueAndSendAsync(User user, EmailTokenPurpose purpose, string? language, CancellationToken cancellationToken)
    {
        var now = Now();
        var lifetime = purpose == EmailTokenPurpose.VerifyEmail
            ? TimeSpan.FromHours(Math.Max(1, _settings.VerifyEmailHours))
            : TimeSpan.FromMinutes(Math.Max(5, _settings.ResetPasswordMinutes));

        // Old password links stop working; long expired ones are removed. Confirmation links stay valid until one of them is used:
        // mail arrives out of order, and a person who asks for a second message often still opens the first one.
        if (purpose != EmailTokenPurpose.VerifyEmail)
        {
            foreach (var open in await _tokens.GetUnusedAsync(user.Id, purpose, cancellationToken))
            {
                open.UsedAt = now;
            }
        }

        await _tokens.DeleteExpiredAsync(now.AddDays(-7), cancellationToken);

        var token = NewToken();
        await _tokens.AddAsync(new EmailToken
        {
            UserId = user.Id,
            Purpose = purpose,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        }, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        var page = purpose == EmailTokenPurpose.VerifyEmail ? "verify-email.html" : "reset-password.html";
        var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/{page}?token={Uri.EscapeDataString(token)}";
        var (subject, text, html) = purpose == EmailTokenPurpose.VerifyEmail
            ? EmailTemplates.Verification(language, user.FirstName, link, (int)lifetime.TotalHours)
            : EmailTemplates.PasswordReset(language, user.FirstName, link, (int)lifetime.TotalMinutes);

        try
        {
            await _sender.SendAsync(new EmailMessage(user.Email, subject, text, html), cancellationToken);
            return true;
        }
        catch (EmailDeliveryException)
        {
            // The sender already logged the kind of failure; nothing identifying is added here.
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", purpose);
            return false;
        }
    }

    /// <summary>Makes a new reset code, keeps its hash and mails it. Older codes of the account stop working at once.</summary>
    private async Task<bool> IssueAndSendCodeAsync(User user, string? language, CancellationToken cancellationToken)
    {
        var now = Now();
        foreach (var old in await _tokens.GetUnusedAsync(user.Id, EmailTokenPurpose.ResetCode, cancellationToken))
        {
            old.UsedAt = now;
        }

        await _tokens.DeleteExpiredAsync(now.AddDays(-7), cancellationToken);

        // Six digits can repeat; the hash column is unique, so a repeat is simply drawn again.
        string code, hash;
        var tries = 0;
        do
        {
            code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            hash = CodeHash(user.Id, code);
        }
        while (await _tokens.GetByHashAsync(hash, EmailTokenPurpose.ResetCode, cancellationToken) is not null && ++tries < 20);

        var token = new EmailToken
        {
            UserId = user.Id,
            Purpose = EmailTokenPurpose.ResetCode,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(Math.Max(1, _settings.ResetCodeMinutes))
        };
        await _tokens.AddAsync(token, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        var (subject, text, html) = EmailTemplates.ResetCode(language, user.FirstName, code, Math.Max(1, _settings.ResetCodeMinutes));

        try
        {
            await _sender.SendAsync(new EmailMessage(user.Email, subject, text, html), cancellationToken);
            return true;
        }
        catch (EmailDeliveryException)
        {
            // A code nobody received is closed, so it cannot be guessed at for nothing. Nothing identifying is logged.
            token.UsedAt = Now();
            await _tokens.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", EmailTokenPurpose.ResetCode);
            return false;
        }
    }

    /// <summary>How long to wait before another e-mail of this kind may go to the account (zero when it may go now).</summary>
    private async Task<TimeSpan> RetryAfterAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var latest = await _tokens.GetLatestCreatedAtAsync(userId, purpose, cancellationToken);
        if (latest is null)
        {
            return TimeSpan.Zero;
        }

        var left = latest.Value + TimeSpan.FromSeconds(Math.Max(0, _settings.EmailCooldownSeconds)) - Now();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>256 random bits in URL-safe Base64.</summary>
    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>HMAC of the code and the account: the same six digits for two accounts give two different values.</summary>
    private string CodeHash(int userId, string code)
        => Convert.ToHexString(HMACSHA256.HashData(_codeKey, Encoding.UTF8.GetBytes($"{userId}:{code}"))).ToLowerInvariant();
}
