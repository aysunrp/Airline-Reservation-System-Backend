using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Media;
using Service.Helpers.DTOs.SiteContent;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/sight-media")]
[Produces("application/json")]
public class AdminSightMediaController : ControllerBase
{
    private readonly ISightMediaService _sights;
    private readonly IAuditLogService _audit;

    public AdminSightMediaController(ISightMediaService sights, IAuditLogService audit)
    {
        _sights = sights;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SiteContentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SiteContentDto>>> List(CancellationToken cancellationToken)
        => Ok(await _sights.GetOverridesAsync(cancellationToken));

    [HttpPost("upload")]
    [RequestSizeLimit(8_000_000)]
    [ProducesResponseType<MediaUploadResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaUploadResultDto>> Upload([FromQuery] string citySlug, [FromQuery] int sightIndex, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _sights.UploadAsync(citySlug, sightIndex, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SightMediaUpdated, "SiteContent", $"{citySlug}.{sightIndex}", result.RelativePath, cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromQuery] string citySlug, [FromQuery] int sightIndex, CancellationToken cancellationToken)
    {
        await _sights.DeleteAsync(citySlug, sightIndex, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SightMediaRemoved, "SiteContent", $"{citySlug}.{sightIndex}", null, cancellationToken);
        return NoContent();
    }
}
