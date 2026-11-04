using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.LandingHeroVideo;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/hero-videos")]
[Produces("application/json")]
public class AdminLandingHeroVideosController : ControllerBase
{
    private readonly ILandingHeroVideoService _videos;
    private readonly IHeroMediaStorage _media;
    private readonly IAuditLogService _audit;

    public AdminLandingHeroVideosController(ILandingHeroVideoService videos, IHeroMediaStorage media, IAuditLogService audit)
    {
        _videos = videos;
        _media = media;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LandingHeroVideoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LandingHeroVideoDto>>> List(CancellationToken cancellationToken)
        => Ok(await _videos.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<LandingHeroVideoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandingHeroVideoDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _videos.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<LandingHeroVideoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LandingHeroVideoDto>> Create([FromBody] LandingHeroVideoRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _videos.CreateAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoCreated, "LandingHeroVideo", created.Id.ToString(), created.Title, cancellationToken);
        if (created.IsActive)
        {
            await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoActivated, "LandingHeroVideo", created.Id.ToString(), created.Title, cancellationToken);
        }

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<LandingHeroVideoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandingHeroVideoDto>> Update(int id, [FromBody] LandingHeroVideoRequestDto request, CancellationToken cancellationToken)
    {
        var before = await _videos.GetByIdAsync(id, cancellationToken);
        var updated = await _videos.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoUpdated, "LandingHeroVideo", id.ToString(), updated.Title, cancellationToken);
        if (!before.IsActive && updated.IsActive)
        {
            await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoActivated, "LandingHeroVideo", id.ToString(), updated.Title, cancellationToken);
        }

        return Ok(updated);
    }

    [HttpPost("{id:int}/activate")]
    [ProducesResponseType<LandingHeroVideoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandingHeroVideoDto>> Activate(int id, CancellationToken cancellationToken)
    {
        var updated = await _videos.SetActiveAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoActivated, "LandingHeroVideo", id.ToString(), updated.Title, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _videos.GetByIdAsync(id, cancellationToken);
        await _videos.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HeroVideoDeleted, "LandingHeroVideo", id.ToString(), existing.Title, cancellationToken);
        return NoContent();
    }

    [HttpPost("upload/video")]
    [RequestSizeLimit(85_000_000)]
    [ProducesResponseType(typeof(HeroMediaUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HeroMediaUploadResponse>> UploadVideo(IFormFile file, CancellationToken cancellationToken)
    {
        var path = await _media.SaveVideoAsync(file, cancellationToken);
        return Ok(new HeroMediaUploadResponse(path));
    }

    [HttpPost("upload/poster")]
    [RequestSizeLimit(10_000_000)]
    [ProducesResponseType(typeof(HeroMediaUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HeroMediaUploadResponse>> UploadPoster(IFormFile file, CancellationToken cancellationToken)
    {
        var path = await _media.SavePosterAsync(file, cancellationToken);
        return Ok(new HeroMediaUploadResponse(path));
    }

    public record HeroMediaUploadResponse(string Url);
}
