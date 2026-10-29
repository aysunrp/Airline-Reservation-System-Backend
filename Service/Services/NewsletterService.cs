using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Newsletter subscription with double opt-in. The confirmation link carries 256 random bits shown only in the e-mail; the database keeps
/// its SHA-256 hash, which dies when the link is used or expires. The unsubscribe link carries the subscriber's random public key and a
/// signature (HMAC with a server secret), so it needs no login and cannot be guessed. Neither a token, an address nor an e-mail is
/// written to the log.
/// </summary>
public class NewsletterService : INewsletterService
{
    private const string InvalidLinkMessage = "This link is invalid or has expired.";
    private const string EmailUnavailableMessage = "We cannot send e-mail right now. Please try again later.";

    private readonly INewsletterSubscriberRepository _subscribers;
    private readonly IEmailSender _sender;
    private readonly IValidator<NewsletterSubscribeRequestDto> _subscribeValidator;
    private readonly IValidator<NewsletterTokenRequestDto> _tokenValidator;
    private readonly NewsletterSettings _settings;
    private readonly AuthSettings _auth;
    private readonly byte[] _linkKey;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NewsletterService> _logger;
    private readonly IAdminAlertService? _adminAlerts;

    public NewsletterService(
        INewsletterSubscriberRepository subscribers,
        IEmailSender sender,
        IValidator<NewsletterSubscribeRequestDto> subscribeValidator,
        IValidator<NewsletterTokenRequestDto> tokenValidator,
        IOptions<NewsletterSettings> settings,
        IOptions<AuthSettings> auth,
        IOptions<JwtSettings> jwt,
        TimeProvider timeProvider,
        ILogger<NewsletterService> logger,
        IAdminAlertService? adminAlerts = null)
    {
        _adminAlerts = adminAlerts;
        _subscribers = subscribers;
        _sender = sender;
        _subscribeValidator = subscribeValidator;
        _tokenValidator = tokenValidator;
        _settings = settings.Value;
        _auth = auth.Value;
        // The signature of unsubscribe links comes from the server's secret: a copy of the database alone cannot make one.
        _linkKey = SHA256.HashData(Encoding.UTF8.GetBytes("aerova-newsletter-unsubscribe|" + jwt.Value.Secret));
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<NewsletterSubscribeResponseDto> SubscribeAsync(NewsletterSubscribeRequestDto request, CancellationToken cancellationToken = default)
    {
        await _subscribeValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var email = request.Email.Trim().ToLowerInvariant();
        var language = EmailTemplates.NormalizeLanguage(request.Language);
        var response = new NewsletterSubscribeResponseDto(NewsletterSubscribeResponseDto.ConfirmationSent);

        var subscriber = await _subscribers.GetByEmailAsync(email, cancellationToken);
        if (subscriber is { Status: NewsletterStatus.Active })
        {
            // Already subscribed: the owner is told by e-mail, the visitor sees the same answer as for a new address.
            var (subject, text, html) = NewsletterEmailTemplates.AlreadySubscribed(subscriber.Language, UnsubscribeLink(subscriber));
            await TrySendAsync(new EmailMessage(subscriber.Email, subject, text, html), cancellationToken);
            return response;
        }

        if (subscriber is { Status: NewsletterStatus.Pending, ConfirmationSentAt: { } sentAt, ConfirmationExpiresAt: { } expires }
            && expires > now && now - sentAt < TimeSpan.FromMinutes(Math.Max(0, _settings.ResendCooldownMinutes)))
        {
            // A confirmation e-mail went out a moment ago: no second one.
            return response;
        }

        if (subscriber is null)
        {
            subscriber = new NewsletterSubscriber { Email = email, Language = language, CreatedAt = now };
            await _subscribers.AddAsync(subscriber, cancellationToken);
        }

        // A new, waiting or unsubscribed address gets a new link; an unsubscribed one stays out until the link is opened.
        var token = NewToken();
        subscriber.Status = NewsletterStatus.Pending;
        subscriber.Language = language;
        subscriber.ConfirmationTokenHash = Hash(token);
        subscriber.ConfirmationExpiresAt = now.AddHours(Math.Max(1, _settings.ConfirmationHours));
        subscriber.ConfirmationSentAt = null;

        try
        {
            await _subscribers.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests for a new address at once: the other one stored it and sends the e-mail.
            return response;
        }

        var link = $"{_auth.FrontendBaseUrl.TrimEnd('/')}/newsletter-confirm.html?token={Uri.EscapeDataString(token)}";
        var (confirmSubject, confirmText, confirmHtml) = NewsletterEmailTemplates.Confirmation(language, link, Math.Max(1, _settings.ConfirmationHours), UnsubscribeLink(subscriber));
        if (!await TrySendAsync(new EmailMessage(subscriber.Email, confirmSubject, confirmText, confirmHtml), cancellationToken))
        {
            // Never say the e-mail was sent when it was not; the next request may try again at once.
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        subscriber.ConfirmationSentAt = now;
        await _subscribers.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<NewsletterResultDto> ConfirmAsync(NewsletterTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        await _tokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var subscriber = await _subscribers.GetByConfirmationHashAsync(Hash(request.Token.Trim()), cancellationToken);
        if (subscriber is null)
        {
            // Unknown, or already used: a used link has no hash any more.
            throw new BusinessRuleException(InvalidLinkMessage, "link_invalid");
        }

        if (subscriber.ConfirmationExpiresAt is not { } expires || expires <= now)
        {
            throw new BusinessRuleException(InvalidLinkMessage, "link_expired");
        }

        subscriber.Status = NewsletterStatus.Active;
        subscriber.ConfirmedAt = now;
        subscriber.UnsubscribedAt = null;
        subscriber.ConfirmationTokenHash = null;
        subscriber.ConfirmationExpiresAt = null;
        await _subscribers.SaveChangesAsync(cancellationToken);

        if (_adminAlerts is not null)
        {
            await _adminAlerts.QueueAndSaveAsync($"newsletter:{subscriber.Id}:confirmed:{now.Ticks}", "New newsletter subscriber",
                $"{subscriber.Email} confirmed the newsletter subscription (language {subscriber.Language}).", cancellationToken);
        }

        return new NewsletterResultDto("confirmed");
    }

    public async Task<NewsletterResultDto> UnsubscribeAsync(NewsletterTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        await _tokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var subscriber = await FindByUnsubscribeTokenAsync(request.Token.Trim(), cancellationToken)
                         ?? throw new BusinessRuleException(InvalidLinkMessage, "link_invalid");

        if (subscriber.Status != NewsletterStatus.Unsubscribed)
        {
            subscriber.Status = NewsletterStatus.Unsubscribed;
            subscriber.UnsubscribedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // A confirmation link sent earlier must not bring the address back.
            subscriber.ConfirmationTokenHash = null;
            subscriber.ConfirmationExpiresAt = null;
            await _subscribers.SaveChangesAsync(cancellationToken);
        }

        return new NewsletterResultDto("unsubscribed");
    }

    public async Task<IReadOnlyList<NewsletterRecipient>> GetRecipientsAsync(CancellationToken cancellationToken = default)
        => (await _subscribers.GetActiveAsync(cancellationToken))
            .Select(s => new NewsletterRecipient(s.Email, s.Language, UnsubscribeLink(s)))
            .ToList();

    /// <summary>The link every newsletter e-mail carries: the key of the subscriber and the signature of that key.</summary>
    public string UnsubscribeLinkFor(NewsletterSubscriber subscriber) => UnsubscribeLink(subscriber);

    internal string UnsubscribeLink(NewsletterSubscriber subscriber)
        => $"{_auth.FrontendBaseUrl.TrimEnd('/')}/newsletter-unsubscribe.html?token={Uri.EscapeDataString(UnsubscribeToken(subscriber.UnsubscribeKey))}";

    internal string UnsubscribeToken(Guid key)
    {
        var id = key.ToString("N");
        return id + "." + Sign(id);
    }

    private async Task<NewsletterSubscriber?> FindByUnsubscribeTokenAsync(string token, CancellationToken cancellationToken)
    {
        var parts = token.Split('.');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var key))
        {
            return null;
        }

        // The signature is checked before the database is asked, so a guessed key costs nothing and shows nothing.
        var expected = Encoding.ASCII.GetBytes(Sign(parts[0]));
        var given = Encoding.ASCII.GetBytes(parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(expected, given))
        {
            return null;
        }

        return await _subscribers.GetByUnsubscribeKeyAsync(key, cancellationToken);
    }

    private string Sign(string id)
        => Convert.ToBase64String(HMACSHA256.HashData(_linkKey, Encoding.ASCII.GetBytes("unsubscribe|" + id))).TrimEnd('=').Replace('+', '-').Replace('/', '_')[..22];

    private async Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _sender.SendAsync(message, cancellationToken);
            return true;
        }
        catch (EmailDeliveryException)
        {
            // The sender already logged the kind of failure; neither the address nor the content is added here.
            _logger.LogWarning("A newsletter e-mail could not be delivered.");
            return false;
        }
    }

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
