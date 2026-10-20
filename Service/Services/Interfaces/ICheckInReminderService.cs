namespace Service.Services.Interfaces;

public sealed record CheckInReminderResult(int Reminders);

public interface ICheckInReminderService
{
    /// <summary>
    /// Sends a check-in reminder to the owner of every confirmed booking whose first flight departs within the reminder window
    /// (24 hours by default). Each booking is reminded once.
    /// </summary>
    Task<CheckInReminderResult> QueueDueAsync(CancellationToken cancellationToken = default);
}
