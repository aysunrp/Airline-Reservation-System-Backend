using System.Security.Claims;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AirlineReservationSystem.Hubs;

/// <summary>
/// The real-time channel of the Support Center. Only signed-in accounts connect (the sign-in token is checked as for any request, including
/// that the account still exists and is active). The server alone decides who is in which group: a connection joins its own account group
/// and, for an Admin or SuperAdmin, the staff group. The hub has no method a browser can call, so nobody can join a group or ask for
/// another customer's events: it only receives what the server sends to those groups.
/// </summary>
[Authorize]
public sealed class SupportHub : Hub
{
    public const string Path = "/hubs/support";
    public const string StaffGroup = "support-staff";
    public const string EventName = "supportEvent";

    public static string UserGroup(int userId) => "user:" + userId;

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        if (user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.SuperAdmin))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        }

        // The token is checked when the connection starts; the connection ends when the token does, so an expired sign-in stops receiving.
        if (long.TryParse(user.FindFirstValue("exp"), out var expires))
        {
            var wait = DateTimeOffset.FromUnixTimeSeconds(expires) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero && wait < TimeSpan.FromDays(40))
            {
                var connection = Context;
                _ = Task.Delay(wait, connection.ConnectionAborted).ContinueWith(task =>
                {
                    if (!task.IsCanceled)
                    {
                        connection.Abort();
                    }
                }, TaskScheduler.Default);
            }
        }

        await base.OnConnectedAsync();
    }
}
