using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.SiteContent;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>The editable Azerbaijani texts of a static site page (for example "about" for the AEROVA haqqında page).</summary>
[ApiController]
[Route("api/site-content")]
[Produces("application/json")]
public class SiteContentController : ControllerBase
{
    private readonly ISiteContentService _contents;

    public SiteContentController(ISiteContentService contents)
    {
        _contents = contents;
    }

    /// <summary>All texts of the page, each with its key (404 for a page with no content).</summary>
    [HttpGet("{page}")]
    [ProducesResponseType<IReadOnlyList<SiteContentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SiteContentDto>>> GetPage(string page, CancellationToken cancellationToken)
        => Ok(await _contents.GetPageAsync(page, cancellationToken));
}
