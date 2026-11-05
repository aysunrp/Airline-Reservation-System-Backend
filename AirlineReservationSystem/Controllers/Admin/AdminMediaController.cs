using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// The media library of the website: every uploaded file with the places that show it, replacing an image in place, deleting a file nothing
/// uses, and the pictures that have a fixed place on a page (the logo, the About page photos).
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/media")]
[Produces("application/json")]
public class AdminMediaController : ControllerBase
{
    private readonly IAdminMediaService _media;
    private readonly IAuditLogService _audit;

    public AdminMediaController(IAdminMediaService media, IAuditLogService audit)
    {
        _media = media;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MediaFileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MediaFileDto>>> List([FromQuery] string? category, [FromQuery] bool unusedOnly, CancellationToken cancellationToken)
        => Ok(await _media.ListAsync(category, unusedOnly, cancellationToken));

    [HttpPut("file")]
    [RequestSizeLimit(12_000_000)]
    [ProducesResponseType<MediaFileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaFileDto>> Replace([FromQuery] string path, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _media.ReplaceAsync(path, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.MediaFileReplaced, "Media", result.Path, null, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("file")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromQuery] string path, CancellationToken cancellationToken)
    {
        await _media.DeleteAsync(path, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.MediaFileDeleted, "Media", path, null, cancellationToken);
        return NoContent();
    }

    [HttpGet("slots")]
    [ProducesResponseType<IReadOnlyList<SiteImageSlotDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SiteImageSlotDto>>> Slots(CancellationToken cancellationToken)
        => Ok(await _media.GetSlotsAsync(cancellationToken));

    [HttpPost("slots/{slot}")]
    [RequestSizeLimit(12_000_000)]
    [ProducesResponseType<SiteImageSlotDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteImageSlotDto>> SetSlot(string slot, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _media.SetSlotAsync(slot, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SiteImageUpdated, "SiteImage", slot, result.Path, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("slots/{slot}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearSlot(string slot, CancellationToken cancellationToken)
    {
        await _media.ClearSlotAsync(slot, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SiteImageRemoved, "SiteImage", slot, null, cancellationToken);
        return NoContent();
    }
}
