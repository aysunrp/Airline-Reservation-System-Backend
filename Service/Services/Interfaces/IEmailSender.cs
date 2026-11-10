namespace Service.Services.Interfaces;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Raised when a message could not be delivered. It carries no server text, address or secret.</summary>
public class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message) : base(message)
    {
    }
}

public interface IEmailSender
{
    /// <summary>False until the SMTP settings (host, login, app password, sender) are all present.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends the message or throws <see cref="EmailDeliveryException"/>.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
