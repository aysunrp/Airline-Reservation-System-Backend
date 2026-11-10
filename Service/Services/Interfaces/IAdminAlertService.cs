namespace Service.Services.Interfaces;

public interface IAdminAlertService
{
    /// <summary>
    /// Adds a message for the admin to the current unit of work (the caller saves it together with the event). The same <paramref name="eventKey"/>
    /// never adds a second message. Does nothing when admin e-mails are switched off or no admin address is set.
    /// </summary>
    Task QueueAsync(string eventKey, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>Same, and saves at once. Never throws: a message for the admin must not break the event it reports.</summary>
    Task QueueAndSaveAsync(string eventKey, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>Sends the messages that are waiting; returns how many were sent. Nothing is sent while e-mail or the admin address is not set up.</summary>
    Task<int> SendDueAsync(CancellationToken cancellationToken = default);
}
