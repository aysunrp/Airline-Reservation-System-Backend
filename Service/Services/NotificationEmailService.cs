using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Sends each notification of an account to that account's own e-mail address, so a traveller who registered with a Gmail address gets
/// the booking news there. Only an address that was verified receives anything, and nothing is sent to the contact address typed into
/// a booking. Notifications are picked up shortly after they are saved, one e-mail each; a message that cannot be delivered is tried a
/// few times and then left alone (the notification stays in the app). Notifications older than a day are never e-mailed, so switching
/// e-mail on later does not flood anyone with old news. Only counts are logged: no address, name or text.
/// </summary>
public class NotificationEmailService : INotificationEmailService
{
    private const int MaxAttempts = 3;
    private const int BatchSize = 20;
    private static readonly TimeSpan FreshFor = TimeSpan.FromDays(1);

    private readonly INotificationRepository _notifications;
    private readonly IBookingRepository _bookings;
    private readonly IEmailSender _sender;
    private readonly AuthSettings _auth;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationEmailService> _logger;
    private readonly IAdminAlertService? _adminAlerts;

    public NotificationEmailService(
        INotificationRepository notifications,
        IBookingRepository bookings,
        IEmailSender sender,
        IOptions<AuthSettings> auth,
        TimeProvider timeProvider,
        ILogger<NotificationEmailService> logger,
        IAdminAlertService? adminAlerts = null)
    {
        _adminAlerts = adminAlerts;
        _notifications = notifications;
        _bookings = bookings;
        _sender = sender;
        _auth = auth.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_sender.IsConfigured)
        {
            return 0;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var due = await _notifications.GetDueForEmailAsync(now - FreshFor, MaxAttempts, BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var link = $"{_auth.FrontendBaseUrl.TrimEnd('/')}/notifications.html";
        var supportLink = $"{_auth.FrontendBaseUrl.TrimEnd('/')}/support.html#requests";
        int sent = 0, failed = 0;

        foreach (var notification in due)
        {
            var (subject, text, html) = await RefundEmailAsync(notification, cancellationToken)
                                        ?? EmailTemplates.Notification(notification.User.FirstName, notification.Title, notification.Message,
                                            notification.Type == NotificationType.SupportUpdate ? supportLink : link);

            try
            {
                await _sender.SendAsync(new EmailMessage(notification.User.Email, subject, text, html), cancellationToken);
                notification.EmailSentAt = _timeProvider.GetUtcNow().UtcDateTime;
                sent++;
            }
            catch (EmailDeliveryException)
            {
                notification.EmailAttempts++;
                failed++;

                // After the last try the admin is told that a customer e-mail could not be delivered (no address or text in the message).
                if (notification.EmailAttempts >= MaxAttempts && _adminAlerts is not null)
                {
                    await _adminAlerts.QueueAsync($"emailfail:notification:{notification.Id}", "Customer e-mail could not be delivered",
                        $"A {notification.Type} e-mail (notification {notification.Id}, booking {notification.BookingId}) failed {MaxAttempts} times and was given up. The notification stays in the app.", cancellationToken);
                }
            }

            // Saved one by one: a message that was sent is never sent again because a later one failed.
            _notifications.Update(notification);
            await _notifications.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Notification e-mails: {Sent} sent, {Failed} could not be delivered.", sent, failed);
        return sent;
    }

    /*
     * A cancellation or refund notification becomes the detailed e-mail: the booking, its flights and the amounts the server saved when it
     * was cancelled (never anything a page sent), in the language the booking was cancelled in. The kind comes from the notification, so a
     * "started" e-mail sent late still says "started". Without the booking it falls back to the plain notification e-mail.
     */
    private async Task<(string Subject, string Text, string Html)?> RefundEmailAsync(Notification notification, CancellationToken cancellationToken)
    {
        RefundEmailKind? kind = notification.Type switch
        {
            NotificationType.BookingCancelled => RefundEmailKind.Cancelled,
            NotificationType.RefundPending => RefundEmailKind.RefundPending,
            NotificationType.RefundCompleted => RefundEmailKind.RefundCompleted,
            NotificationType.RefundFailed => RefundEmailKind.RefundFailed,
            _ => null
        };
        if (kind is null || notification.Booking is null)
        {
            return null;
        }

        var booking = await _bookings.GetByPnrAsync(notification.Booking.Pnr, cancellationToken);
        var cancellation = booking?.Cancellation;
        if (booking is null || cancellation is null)
        {
            return null;
        }

        var refund = cancellation.Refunds.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        var payment = refund is null ? null : booking.Payments.FirstOrDefault(p => p.Id == refund.PaymentId);
        var details = new RefundEmailDetails(
            booking.Pnr,
            booking.Flights.OrderBy(f => f.LegOrder)
                .Select(f => new RefundEmailFlight(f.Flight.FlightNumber, f.Flight.Route.Origin.Code, f.Flight.Route.Destination.Code, f.Flight.DepartureAt))
                .ToList(),
            cancellation.RequestedAt,
            cancellation.Reason,
            cancellation.AmountPaid,
            cancellation.FeeAmount,
            cancellation.RefundAmount,
            refund?.Currency ?? payment?.Currency ?? Domain.Constants.AppConstants.Currency,
            refund is not null && refund.Status != RefundStatus.NotEligible && cancellation.RefundAmount > 0,
            payment?.Method == PaymentMethod.StripeCheckout,
            $"{_auth.FrontendBaseUrl.TrimEnd('/')}/my-trips.html");

        return RefundEmailTemplate.Build(cancellation.Language, kind.Value, notification.User.FirstName, details);
    }
}
