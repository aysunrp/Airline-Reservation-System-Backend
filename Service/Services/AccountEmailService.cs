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
/// Links sent by e-mail. A token is 256 random bits shown only in the e-mail; the database keeps its SHA-256 hash, an expiry and the
/// moment it was used. Neither the token, the link, the address nor the SMTP login is ever written to the log.
/// </summary>
public class AccountEmailService : IAccountEmailService
{
    private const string InvalidLinkMessage = "This link is invalid or has expired.";
    private const string EmailUnavailableMessage = "We cannot send e-mail right now. Please try again later.";

    private readonly IUserRepository _users;
    private readonly IEmailTokenRepository _tokens;
    private readonly IEmailSender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginAttemptTracker _attempts;
    private readonly IValidator<VerifyEmailRequestDto> _verifyValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetValidator;
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

    public async Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        await _verifyValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = Now();
        var token = await _tokens.GetByHashAsync(Hash(request.Token.Trim()), EmailTokenPurpose.VerifyEmail, cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= now)
        {
            throw new BusinessRuleException(InvalidLinkMessage);
        }

        token.UsedAt = now;
        token.User.EmailVerifiedAt ??= now;
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

        // The same for everybody: whether e-mail works does not depend on the address that was typed.
        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var user = await _users.GetByEmailAsync(AuthService.NormalizeEmail(request.Email), cancellationToken);

        // From here on the caller learns nothing: no account, a disabled account, a mail sent a moment ago and a failed
        // delivery all end the same way as a sent reset link.
        if (user is null || !user.IsActive || await RetryAfterAsync(user.Id, EmailTokenPurpose.ResetPassword, cancellationToken) > TimeSpan.Zero)
        {
            return;
        }

        await IssueAndSendAsync(user, EmailTokenPurpose.ResetPassword, request.Language, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await _resetValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = Now();
        var token = await _tokens.GetByHashAsync(Hash(request.Token.Trim()), EmailTokenPurpose.ResetPassword, cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= now || !token.User.IsActive)
        {
            throw new BusinessRuleException(InvalidLinkMessage);
        }

        var user = token.User;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        // Opening the link that was sent to the address shows the person controls it.
        user.EmailVerifiedAt ??= now;
        _users.Update(user);

        // This link and any other reset link still open are used up.
        foreach (var open in await _tokens.GetUnusedAsync(user.Id, EmailTokenPurpose.ResetPassword, cancellationToken))
        {
            open.UsedAt = now;
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

        // Old links of the same kind stop working; long expired ones are removed.
        foreach (var open in await _tokens.GetUnusedAsync(user.Id, purpose, cancellationToken))
        {
            open.UsedAt = now;
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
}
