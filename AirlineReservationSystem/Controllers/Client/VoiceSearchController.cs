using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Flight;
using Service.Services.VoiceSearch;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Voice flight search: turns the words recognised in the browser into search criteria.</summary>
[ApiController]
[Route("api/voice-search")]
[Produces("application/json")]
public class VoiceSearchController : ControllerBase
{
    private readonly IVoiceSearchService _voiceSearch;

    public VoiceSearchController(IVoiceSearchService voiceSearch)
    {
        _voiceSearch = voiceSearch;
    }

    /// <summary>
    /// Reads a request such as "flights from Baku to London tomorrow morning" (Azerbaijani, English or Russian) into an origin, a destination,
    /// a day or period, a part of the day, a cabin, passengers and a wish for the cheapest flight. It only reads: it searches and books nothing,
    /// and anything unclear comes back as an issue to clear up. Open to visitors; limited per IP address like the other look-ups.
    /// </summary>
    [HttpPost("interpret")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<VoiceSearchInterpretationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<VoiceSearchInterpretationDto>> Interpret(VoiceSearchRequestDto request, CancellationToken cancellationToken)
        => Ok(await _voiceSearch.InterpretAsync(request, cancellationToken));
}
