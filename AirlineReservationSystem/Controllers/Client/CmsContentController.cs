using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Cms;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>What the public pages show from the admin panel: the home page cards and the support page questions (active ones only).</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class CmsContentController : ControllerBase
{
    private readonly IAdminHomeCardService _cards;
    private readonly IAdminFaqService _faq;

    public CmsContentController(IAdminHomeCardService cards, IAdminFaqService faq)
    {
        _cards = cards;
        _faq = faq;
    }

    /// <summary>The active cards of a section ("why" or "premium"; every section without it), in their display order.</summary>
    [HttpGet("home-cards")]
    [ProducesResponseType<IReadOnlyList<HomeCardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HomeCardDto>>> GetHomeCards([FromQuery] string? section, CancellationToken cancellationToken)
        => Ok(await _cards.GetActiveAsync(section, cancellationToken));

    [HttpGet("faq")]
    [ProducesResponseType<IReadOnlyList<FaqItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FaqItemDto>>> GetFaq(CancellationToken cancellationToken)
        => Ok(await _faq.GetActiveAsync(cancellationToken));
}
