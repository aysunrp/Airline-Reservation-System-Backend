using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Notification;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Notifications page and the unread badge in the account menu.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get(CancellationToken cancellationToken)
        => Ok(await _notificationService.GetAsync(User.GetUserId(), cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
        => Ok(await _notificationService.GetUnreadCountAsync(User.GetUserId(), cancellationToken));

    [HttpPatch("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Removes one notification of the signed-in user.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _notificationService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>"Clear" on the notifications page: removes every notification of the signed-in user.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _notificationService.ClearAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllAsReadAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
