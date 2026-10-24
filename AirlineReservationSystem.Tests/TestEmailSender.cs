using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>Replaces SMTP in the API tests: nothing is sent, the messages are kept for the test to read.</summary>
public sealed class TestEmailSender : IEmailSender
{
    private readonly object _lock = new();
    private readonly List<EmailMessage> _sent = new();

    public bool IsConfigured { get; set; } = true;

    public bool Fail { get; set; }

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_lock)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || Fail)
        {
            throw new EmailDeliveryException("The e-mail could not be sent.");
        }

        lock (_lock)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>The token of the latest message sent to the address (what the person would click).</summary>
    public string TokenFor(string email)
    {
        var message = Sent.Last(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        var start = message.TextBody.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length;
        var end = message.TextBody.IndexOfAny(new[] { '\n', ' ', '\r' }, start);
        return Uri.UnescapeDataString(message.TextBody[start..(end < 0 ? message.TextBody.Length : end)]);
    }
}
