using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Assistant;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Ask AEROVA, the travel assistant of the site.</summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistant;

    public AssistantController(IAssistantService assistant)
    {
        _assistant = assistant;
    }

    /// <summary>
    /// Answers the last question of the conversation. Open to visitors who are not signed in (the chat is on every page), so it is limited
    /// per IP address (TravelAssistant:RateLimitPerMinute) and every message and the whole conversation are size-limited. The conversation
    /// is context only: it cannot change the assistant's rules, and nothing is stored. The assistant cannot see accounts, bookings or
    /// payments and cannot change anything.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Assistant)]
    [ProducesResponseType<AssistantChatResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AssistantChatResponseDto>> Chat(AssistantChatRequestDto request, CancellationToken cancellationToken)
        => Ok(await _assistant.ChatAsync(request, cancellationToken));
}
