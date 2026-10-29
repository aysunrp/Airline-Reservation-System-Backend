using Domain.Enums;

namespace Service.Services.Interfaces;

/// <summary>
/// One real-time event about a support request. It carries no message text and no e-mail address: the page that receives it reads the
/// request again through the API, which checks who is asking. <see cref="EventId"/> is new for every event, so a page can drop a duplicate.
/// </summary>
public sealed record SupportRealtimeEvent(
    Guid EventId,
    /// <summary>ticketCreated, customerReplied (staff), ticketUpdated (staff: someone else changed it), staffReplied, statusChanged (customer).</summary>
    string Type,
    int TicketId,
    string TicketNumber,
    string Status,
    /// <summary>Only in events for staff.</summary>
    string? Subject,
    /// <summary>The topic by name (Booking, Payment...), only in events for staff.</summary>
    string? Category,
    DateTime At);

/// <summary>Pushes support events to connected browsers. The staff group is for Admin and SuperAdmin accounts; a customer gets only the events of their own requests.</summary>
public interface ISupportRealtimePublisher
{
    Task ToStaffAsync(SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default);

    Task ToCustomerAsync(int userId, SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default);
}

/// <summary>Used where there is no real-time channel (tests, tools): events go nowhere.</summary>
public sealed class NullSupportRealtimePublisher : ISupportRealtimePublisher
{
    public Task ToStaffAsync(SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ToCustomerAsync(int userId, SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
