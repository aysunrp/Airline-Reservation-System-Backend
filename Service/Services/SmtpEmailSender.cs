using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Sends mail through SMTP with MailKit (Gmail: smtp.gmail.com:587 with StartTls and an App password).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsComplete;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new EmailDeliveryException("E-mail sending is not configured.");
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail.Trim()));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

            using var client = new SmtpClient { Timeout = Math.Max(5, _settings.TimeoutSeconds) * 1000 };
            await client.ConnectAsync(_settings.Host, _settings.Port, ParseSecurity(_settings.Security), cancellationToken);
            await client.AuthenticateAsync(_settings.UserName, _settings.Password, cancellationToken);
            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Only the kind of failure is logged (for example AuthenticationException for a wrong App password): the server's own text
            // can name the recipient, so it is not written, and neither are the login, the password or the message.
            var status = exception is SmtpCommandException command ? (int)command.StatusCode : (int?)null;
            _logger.LogError("E-mail delivery failed ({ExceptionType}, SMTP status {Status}).", exception.GetType().Name, status);
            throw new EmailDeliveryException("The e-mail could not be sent.");
        }
    }

    private static SecureSocketOptions ParseSecurity(string value)
        => Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var parsed) ? parsed : SecureSocketOptions.StartTls;
}
