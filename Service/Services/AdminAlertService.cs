using System.Data.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// E-mails to the admin about business events. A message is stored with the event and sent by the background job: a failed or slow mail
/// server never blocks a booking or a payment, and a message is tried a few times. Only counts are logged: no address, subject or text.
/// </summary>
public class AdminAlertService : IAdminAlertService
{
    private const int BatchSize = 20;

    private readonly IAdminAlertRepository _alerts;
    private readonly IEmailSender _sender;
    private readonly AdminNotificationSettings _settings;
    private readonly SmtpSettings _smtp;
    private readonly AuthSettings _auth;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminAlertService> _logger;

    public AdminAlertService(
        IAdminAlertRepository alerts,
        IEmailSender sender,
        IOptions<AdminNotificationSettings> settings,
        IOptions<SmtpSettings> smtp,
        IOptions<AuthSettings> auth,
        TimeProvider timeProvider,
        ILogger<AdminAlertService> logger)
    {
        _alerts = alerts;
        _sender = sender;
        _settings = settings.Value;
        _smtp = smtp.Value;
        _auth = auth.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private string Recipient => string.IsNullOrWhiteSpace(_settings.RecipientEmail) ? _smtp.SenderEmail : _settings.RecipientEmail;

    public async Task QueueAsync(string eventKey, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(Recipient))
        {
            return;
        }

        try
        {
            if (await _alerts.KeyExistsAsync(eventKey, cancellationToken))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // The admin alert table is not there yet (the migration was not applied) or the database failed: the event itself goes on,
            // without a message for the admin. Nothing was added to the unit of work, so the caller's save is not affected.
            _logger.LogWarning("Admin alerts are skipped: the alert table could not be read ({ErrorType}).", ex.GetType().Name);
            return;
        }

        await _alerts.AddAsync(new AdminAlert
        {
            EventKey = eventKey.Length > 200 ? eventKey[..200] : eventKey,
            Subject = subject.Length > 200 ? subject[..200] : subject,
            Body = body.Length > 2000 ? body[..2000] : body,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);
    }

    public async Task QueueAndSaveAsync(string eventKey, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            await QueueAsync(eventKey, subject, body, cancellationToken);
            await _alerts.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // A second request stored the same event at the same moment, or the message could not be stored: the event itself is already saved.
            _logger.LogWarning("An admin alert could not be stored ({ErrorType}).", ex.GetType().Name);
        }
    }

    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || !_sender.IsConfigured || string.IsNullOrWhiteSpace(Recipient))
        {
            return 0;
        }

        IReadOnlyList<AdminAlert> due;
        try
        {
            due = await _alerts.GetDueAsync(Math.Max(1, _settings.MaxAttempts), BatchSize, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            _logger.LogWarning("Admin alerts cannot be sent: the alert table could not be read ({ErrorType}).", ex.GetType().Name);
            return 0;
        }

        if (due.Count == 0)
        {
            return 0;
        }

        var link = $"{_auth.FrontendBaseUrl.TrimEnd('/')}/admin.html";
        int sent = 0, failed = 0;
        foreach (var alert in due)
        {
            var subject = "[AEROVA] " + alert.Subject;
            var (text, html) = AdminAlertEmailTemplate.Build(alert.Subject, alert.Body, link);
            try
            {
                await _sender.SendAsync(new EmailMessage(Recipient, subject, text, html), cancellationToken);
                alert.SentAt = _timeProvider.GetUtcNow().UtcDateTime;
                sent++;
            }
            catch (EmailDeliveryException)
            {
                alert.Attempts++;
                failed++;
            }

            // One by one: a message that went out is never sent again because a later one failed.
            _alerts.Update(alert);
            await _alerts.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Admin alerts: {Sent} sent, {Failed} could not be delivered.", sent, failed);
        return sent;
    }
}
