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

    /// <summary>
    /// Connects and signs in to the mail server without sending anything, so the operator can tell whether the settings work.
    /// The answer names the kind of problem in plain words; it never contains the login, the password or the server's own text.
    /// </summary>
    public async Task<(bool Ok, string Message)> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return (false, "E-mail is not set up: the Smtp login, the App password or the sender address is missing.");
        }

        try
        {
            using var client = new SmtpClient { Timeout = Math.Max(5, _settings.TimeoutSeconds) * 1000 };
            await client.ConnectAsync(_settings.Host, _settings.Port, ParseSecurity(_settings.Security), cancellationToken);
            await client.AuthenticateAsync(_settings.UserName, _settings.Password, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return (true, "The mail server accepted the login. E-mail can be sent.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException)
        {
            return (false, "The mail server refused the login (AuthenticationException). The login address or the App password is wrong, or App passwords are not allowed for this account (school and work accounts often block them).");
        }
        catch (Exception exception) when (exception is System.Net.Sockets.SocketException or TimeoutException or SslHandshakeException or IOException)
        {
            return (false, $"The mail server could not be reached ({exception.GetType().Name}). Check the internet connection, a firewall or antivirus that blocks port {_settings.Port}, and the Smtp host and security settings.");
        }
        catch (Exception exception)
        {
            var status = exception is SmtpCommandException command ? (int)command.StatusCode : (int?)null;
            return (false, $"The mail server answered with an error ({exception.GetType().Name}, SMTP status {status?.ToString() ?? "none"}).");
        }
    }

    private static SecureSocketOptions ParseSecurity(string value)
        => Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var parsed) ? parsed : SecureSocketOptions.StartTls;
}
