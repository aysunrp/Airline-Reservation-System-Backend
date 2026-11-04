using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.SiteContent;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>CMS page texts in the admin panel. Seeded keys are edited only; extra keys can be created and removed.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/site-content")]
[Produces("application/json")]
public class AdminSiteContentController : ControllerBase
{
    private readonly ISiteContentService _contents;
    private readonly IAuditLogService _audit;

    public AdminSiteContentController(ISiteContentService contents, IAuditLogService audit)
    {
        _contents = contents;
        _audit = audit;
    }

    /// <summary>The texts of a page with their ids (404 for a page with no content).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SiteContentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SiteContentDto>>> GetPage([FromQuery] string page = "about", CancellationToken cancellationToken = default)
        => Ok(await _contents.GetPageAsync(page, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<SiteContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteContentDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _contents.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<SiteContentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SiteContentDto>> Create([FromBody] SiteContentCreateRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _contents.CreateAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SiteContentCreated, "SiteContent", created.Id.ToString(), $"{created.Page}/{created.Key}", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Edits one text (400 with an Azerbaijani message for an empty or too long text, 404 for an unknown id).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<SiteContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteContentDto>> Update(int id, [FromBody] SiteContentRequestDto? request, CancellationToken cancellationToken)
    {
        var content = await _contents.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SiteContentUpdated, "SiteContent", id.ToString(), $"{content.Page}/{content.Key}", cancellationToken);
        return Ok(content);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _contents.GetByIdAsync(id, cancellationToken);
        await _contents.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SiteContentDeleted, "SiteContent", id.ToString(), $"{existing.Page}/{existing.Key}", cancellationToken);
        return NoContent();
    }
}
