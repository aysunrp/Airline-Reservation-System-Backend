using Microsoft.AspNetCore.SignalR;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Hubs;

/// <summary>Sends support events through SignalR: staff events to the staff group, customer events to that one account's group.</summary>
public sealed class SupportRealtimePublisher : ISupportRealtimePublisher
{
    private readonly IHubContext<SupportHub> _hub;

    public SupportRealtimePublisher(IHubContext<SupportHub> hub)
    {
        _hub = hub;
    }

    public Task ToStaffAsync(SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(SupportHub.StaffGroup).SendAsync(SupportHub.EventName, supportEvent, cancellationToken);

    public Task ToCustomerAsync(int userId, SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(SupportHub.UserGroup(userId)).SendAsync(SupportHub.EventName, supportEvent, cancellationToken);
}
