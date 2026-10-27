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
    private readonly IEmailSender _sender;
    private readonly AuthSettings _auth;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationEmailService> _logger;

    public NotificationEmailService(
        INotificationRepository notifications,
        IEmailSender sender,
        IOptions<AuthSettings> auth,
        TimeProvider timeProvider,
        ILogger<NotificationEmailService> logger)
    {
        _notifications = notifications;
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
        int sent = 0, failed = 0;

        foreach (var notification in due)
        {
            var (subject, text, html) = EmailTemplates.Notification(notification.User.FirstName, notification.Title, notification.Message, link);

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
            }

            // Saved one by one: a message that was sent is never sent again because a later one failed.
            _notifications.Update(notification);
            await _notifications.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Notification e-mails: {Sent} sent, {Failed} could not be delivered.", sent, failed);
        return sent;
    }
}
