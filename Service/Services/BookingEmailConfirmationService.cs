using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// One link per booking, 256 random bits shown only in the e-mail; the database keeps its SHA-256 hash, the booking it belongs to, an
/// expiry (the end of the seat hold) and the moment it was used. Neither a token, a link nor an address is ever written to the log.
/// </summary>
public class BookingEmailConfirmationService : IBookingEmailConfirmationService
{
    private const string EmailUnavailableMessage = "We cannot send e-mail right now. Please try again later.";

    private static readonly TimeSpan DefaultHold = TimeSpan.FromMinutes(15);

    private readonly IBookingRepository _bookings;
    private readonly IEmailTokenRepository _tokens;
    private readonly IEmailSender _sender;
    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingEmailConfirmationService> _logger;

    public BookingEmailConfirmationService(
        IBookingRepository bookings,
        IEmailTokenRepository tokens,
        IEmailSender sender,
        IOptions<AuthSettings> settings,
        TimeProvider timeProvider,
        ILogger<BookingEmailConfirmationService> logger)
    {
        _bookings = bookings;
        _tokens = tokens;
        _sender = sender;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool IsRequired => _settings.RequireBookingEmailConfirmation && _sender.IsConfigured;

    public async Task<bool> SendAsync(Booking booking, string? language, CancellationToken cancellationToken = default)
    {
        if (booking.UserId is null)
        {
            return false;
        }

        var now = Now();

        // A new link replaces the older ones of this booking.
        foreach (var open in await _tokens.GetUnusedForBookingAsync(booking.Id, cancellationToken))
        {
            open.UsedAt = now;
        }

        var token = NewToken();
        await _tokens.AddAsync(new EmailToken
        {
            UserId = booking.UserId.Value,
            BookingId = booking.Id,
            Purpose = EmailTokenPurpose.BookingConfirm,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = booking.ExpiresAt ?? now + DefaultHold
        }, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/confirm-booking.html?token={Uri.EscapeDataString(token)}";
        var name = booking.Passengers.OrderBy(p => p.Id).Select(p => p.FirstName).FirstOrDefault() ?? string.Empty;
        var minutes = Math.Max(1, (int)Math.Ceiling(((booking.ExpiresAt ?? now + DefaultHold) - now).TotalMinutes));
        var (subject, text, html) = EmailTemplates.BookingConfirmation(language, name, booking.Pnr, link, minutes);

        try
        {
            await _sender.SendAsync(new EmailMessage(booking.ContactEmail, subject, text, html), cancellationToken);
            return true;
        }
        catch (EmailDeliveryException)
        {
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", EmailTokenPurpose.BookingConfirm);
            return false;
        }
    }

    public async Task ResendAsync(int userId, string pnr, string? language, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetByPnrAsync(BookingService.NormalizePnr(pnr), cancellationToken);
        if (booking is null || booking.UserId != userId)
        {
            throw new NotFoundException(BookingService.BookingNotFoundMessage);
        }

        if (booking.ContactConfirmedAt is not null)
        {
            throw new ConflictException("This booking is already confirmed.");
        }

        if (booking.Status != BookingStatus.Pending || booking.ExpiresAt <= Now())
        {
            throw new BusinessRuleException("The time to confirm this booking has run out and the seats were released. Please book again.", "booking_closed");
        }

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var latest = await _tokens.GetLatestCreatedAtForBookingAsync(booking.Id, cancellationToken);
        if (latest is not null)
        {
            var wait = latest.Value + TimeSpan.FromSeconds(Math.Max(0, _settings.EmailCooldownSeconds)) - Now();
            if (wait > TimeSpan.Zero)
            {
                throw new TooManyAttemptsException("A message was sent a moment ago. Please wait before asking for another.", wait);
            }
        }

        if (!await SendAsync(booking, language, cancellationToken))
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }
    }

    public async Task ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var value = (token ?? string.Empty).Trim();
        var stored = value.Length is < 20 or > 200
            ? null
            : await _tokens.GetByHashAsync(Hash(value), EmailTokenPurpose.BookingConfirm, cancellationToken);
        if (stored is null || stored.BookingId is null)
        {
            throw new BusinessRuleException("This link is not valid.", "link_invalid");
        }

        var booking = await _bookings.GetByIdAsync(stored.BookingId.Value, cancellationToken);
        if (booking is null)
        {
            throw new BusinessRuleException("This link is not valid.", "link_invalid");
        }

        if (stored.UsedAt is not null)
        {
            throw booking.ContactConfirmedAt is null
                ? new BusinessRuleException("This link was already used. Ask for a new one.", "link_used")
                : new BusinessRuleException("This link was already used. Your booking is confirmed.", "link_confirmed");
        }

        if (stored.ExpiresAt <= now || booking.Status != BookingStatus.Pending)
        {
            throw new BusinessRuleException("This link has expired and the seats were released. Please book again.", "link_expired");
        }

        stored.UsedAt = now;
        booking.ContactConfirmedAt ??= now;
        foreach (var other in await _tokens.GetUnusedForBookingAsync(booking.Id, cancellationToken))
        {
            other.UsedAt = now;
        }

        await _bookings.SaveChangesAsync(cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
