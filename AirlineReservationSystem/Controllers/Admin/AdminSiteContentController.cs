using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.SiteContent;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>The editable texts of the static site pages in the admin panel. Azerbaijani texts are edited, never created or deleted: the page looks them up by key.</summary>
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
}
