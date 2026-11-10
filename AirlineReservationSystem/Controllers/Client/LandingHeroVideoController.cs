using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.LandingHeroVideo;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>The active landing page hero video (public, no auth).</summary>
[ApiController]
[Route("api/landing/hero-video")]
[Produces("application/json")]
public class LandingHeroVideoController : ControllerBase
{
    private readonly ILandingHeroVideoService _videos;

    public LandingHeroVideoController(ILandingHeroVideoService videos)
    {
        _videos = videos;
    }

    /// <summary>Returns the published hero video, or 204 when none is active.</summary>
    [HttpGet]
    [ProducesResponseType<LandingHeroVideoPublicDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<LandingHeroVideoPublicDto>> GetActive(CancellationToken cancellationToken)
    {
        var active = await _videos.GetActivePublicAsync(cancellationToken);
        return active == null ? NoContent() : Ok(active);
    }
}
