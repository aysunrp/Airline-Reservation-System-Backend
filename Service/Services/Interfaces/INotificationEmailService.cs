namespace Service.Services.Interfaces;

public interface INotificationEmailService
{
    /// <summary>
    /// E-mails the notifications that were not sent yet to the address of the account they belong to (booking confirmed, payment,
    /// cancellation, refund, check-in, flight update). Returns how many were sent. Nothing is sent while e-mail is not set up.
    /// </summary>
    Task<int> SendDueAsync(CancellationToken cancellationToken = default);
}
