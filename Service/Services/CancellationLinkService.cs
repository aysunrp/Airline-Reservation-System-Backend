using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// The e-mailed confirmation of a cancellation. The link token is 256 random bits shown only in the e-mail; the database keeps its
/// SHA-256 hash, the booking, the reason and page language that were given, an expiry and the moment it was used. Opening the page of
/// the link cancels nothing by itself: the person presses "confirm" there, so a mail program that opens links cannot cancel a trip.
/// Neither a token, a link nor an address is ever written to the log.
/// </summary>
public class CancellationLinkService : ICancellationLinkService
{
    private const string EmailUnavailableMessage = "We cannot send e-mail right now. Please try again later.";

    private readonly ICancellationService _cancellations;
    private readonly IBookingRepository _bookings;
    private readonly IUserRepository _users;
    private readonly IEmailTokenRepository _tokens;
    private readonly IEmailSender _sender;
    private readonly IValidator<CancelBookingRequestDto> _validator;
    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CancellationLinkService> _logger;
    private readonly IAdminAlertService? _adminAlerts;

    public CancellationLinkService(
        ICancellationService cancellations,
        IBookingRepository bookings,
        IUserRepository users,
        IEmailTokenRepository tokens,
        IEmailSender sender,
        IValidator<CancelBookingRequestDto> validator,
        IOptions<AuthSettings> settings,
        TimeProvider timeProvider,
        ILogger<CancellationLinkService> logger,
        IAdminAlertService? adminAlerts = null)
    {
        _adminAlerts = adminAlerts;
        _cancellations = cancellations;
        _bookings = bookings;
        _users = users;
        _tokens = tokens;
        _sender = sender;
        _validator = validator;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private sealed record Payload(string? Reason, string? Language);

    public async Task<CancelRequestResult> RequestAsync(CancelBookingRequestDto request, int? userId, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // The same checks as cancelling itself (whose booking, already cancelled, already flown), and the estimate the e-mail shows.
        var quote = await _cancellations.GetQuoteAsync(request.Pnr, request.LastName, userId, cancellationToken);
        var booking = await _bookings.GetByPnrAsync(BookingService.NormalizePnr(request.Pnr), cancellationToken)
                      ?? throw new NotFoundException(BookingService.BookingNotFoundMessage);

        var paid = booking.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Paid);
        if (!_settings.RequireCancellationEmailConfirmation || booking.UserId is not int owner || paid is null)
        {
            // A hold that was never paid (or a rule that is switched off) is cancelled at once, as before.
            return new CancelRequestResult(await _cancellations.CancelAsync(request, userId, cancellationToken), null);
        }

        if (!_sender.IsConfigured)
        {
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        var user = await _users.GetByIdAsync(owner, cancellationToken) ?? throw new NotFoundException(BookingService.BookingNotFoundMessage);
        var now = Now();
        var latest = await _tokens.GetLatestCreatedAtForBookingAsync(booking.Id, EmailTokenPurpose.CancelBooking, cancellationToken);
        if (latest is not null)
        {
            var wait = latest.Value + TimeSpan.FromSeconds(Math.Max(0, _settings.EmailCooldownSeconds)) - now;
            if (wait > TimeSpan.Zero)
            {
                throw new TooManyAttemptsException("A confirmation link was sent a moment ago. Please wait before asking for another.", wait);
            }
        }

        // A new link replaces the older ones of this booking.
        foreach (var open in await _tokens.GetUnusedForBookingAsync(booking.Id, EmailTokenPurpose.CancelBooking, cancellationToken))
        {
            open.UsedAt = now;
        }

        var minutes = Math.Max(5, _settings.CancelLinkMinutes);
        var language = string.IsNullOrWhiteSpace(request.Language) ? null : EmailTemplates.NormalizeLanguage(request.Language);
        var token = NewToken();
        await _tokens.AddAsync(new EmailToken
        {
            UserId = owner,
            BookingId = booking.Id,
            Purpose = EmailTokenPurpose.CancelBooking,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes),
            Payload = JsonSerializer.Serialize(new Payload(string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(), language))
        }, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);

        if (_adminAlerts is not null)
        {
            await _adminAlerts.QueueAndSaveAsync($"booking:{booking.Id}:cancel-requested:{now.Ticks}", "Cancellation requested",
                $"A cancellation of booking {booking.Pnr} was requested. It is cancelled only when the customer confirms the link sent by e-mail.\n\nBooking id: {booking.Id}", cancellationToken);
        }

        var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/cancel-confirm.html?token={Uri.EscapeDataString(token)}";
        var legs = booking.Flights.OrderBy(f => f.LegOrder)
            .Select(f => $"{f.Flight.FlightNumber} {f.Flight.Route.Origin.Code} → {f.Flight.Route.Destination.Code}")
            .ToList();
        var (subject, text, html) = EmailTemplates.CancelConfirmation(language, user.FirstName, booking.Pnr, string.Join(", ", legs),
            quote.RefundAmount, quote.FeeAmount, paid.Currency, quote.IsRefundable && quote.RefundAmount > 0, link, minutes);

        try
        {
            await _sender.SendAsync(new EmailMessage(user.Email, subject, text, html), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            _logger.LogWarning("A {Purpose} e-mail could not be delivered.", EmailTokenPurpose.CancelBooking);
            throw new ServiceUnavailableException(EmailUnavailableMessage);
        }

        return new CancelRequestResult(null, new CancellationLinkDto(true, Mask(user.Email), minutes));
    }

    public async Task<BookingDto> ConfirmAsync(ConfirmCancellationRequestDto request, CancellationToken cancellationToken = default)
    {
        var value = (request.Token ?? string.Empty).Trim();
        var stored = value.Length is < 20 or > 200 ? null : await _tokens.GetByHashAsync(Hash(value), EmailTokenPurpose.CancelBooking, cancellationToken);
        if (stored?.BookingId is null)
        {
            throw new BusinessRuleException("This link is not valid.", "link_invalid");
        }

        var booking = await _bookings.GetByIdAsync(stored.BookingId.Value, cancellationToken)
                      ?? throw new BusinessRuleException("This link is not valid.", "link_invalid");

        if (stored.UsedAt is not null)
        {
            throw booking.Status == BookingStatus.Cancelled
                ? new BusinessRuleException("This link was already used. The booking is cancelled.", "link_confirmed")
                : new BusinessRuleException("This link was already used. Ask for a new one from your booking.", "link_used");
        }

        if (stored.ExpiresAt <= Now())
        {
            throw new BusinessRuleException("This link has expired. Ask for a new one from your booking.", "link_expired");
        }

        Payload? payload = null;
        try
        {
            payload = string.IsNullOrEmpty(stored.Payload) ? null : JsonSerializer.Deserialize<Payload>(stored.Payload);
        }
        catch (JsonException)
        {
            payload = null;
        }

        // Spent together with the cancellation: it is saved in the same unit of work, and a refused cancellation saves neither.
        stored.UsedAt = Now();
        var cancelled = await _cancellations.CancelOwnBookingAsync(booking.Pnr, stored.UserId, payload?.Reason,
            string.IsNullOrWhiteSpace(request.Language) ? payload?.Language : request.Language, cancellationToken);
        await _tokens.SaveChangesAsync(cancellationToken);
        return cancelled;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>"a****@gmail.com": enough to recognise the address, not to read it.</summary>
    internal static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return email[0] + new string('*', Math.Max(3, at - 1)) + email[at..];
    }

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
