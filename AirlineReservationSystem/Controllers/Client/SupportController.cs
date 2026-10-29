using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Support;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>
/// The Support page: contact details, and the signed-in customer's own requests. Whose request it is comes from the sign-in token only;
/// no request carries a user id, and another customer's request answers 404.
/// </summary>
[ApiController]
[Route("api/support")]
[Produces("application/json")]
public class SupportController : ControllerBase
{
    private readonly ISupportService _support;

    public SupportController(ISupportService support)
    {
        _support = support;
    }

    /// <summary>How to reach AEROVA: only what is configured (empty when nothing is).</summary>
    [HttpGet("info")]
    [ProducesResponseType<SupportInfoDto>(StatusCodes.Status200OK)]
    public ActionResult<SupportInfoDto> GetInfo() => Ok(_support.GetInfo());

    /// <summary>Opens a request. The same request sent again within a short time is the same request (no second ticket or e-mail).</summary>
    [HttpPost("tickets")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Support)]
    [ProducesResponseType<SupportTicketDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SupportTicketDetailDto>> Create(CreateSupportTicketRequestDto request, CancellationToken cancellationToken)
    {
        var ticket = await _support.CreateAsync(User.GetUserId(), request, cancellationToken);
        return Created($"/api/support/tickets/{ticket.Id}", ticket);
    }

    /// <summary>The signed-in customer's requests, most recently active first.</summary>
    [HttpGet("tickets")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<SupportTicketDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupportTicketDto>>> GetMine(CancellationToken cancellationToken)
        => Ok(await _support.GetMineAsync(User.GetUserId(), cancellationToken));

    /// <summary>One request with its conversation; 404 when it is not the customer's.</summary>
    [HttpGet("tickets/{id:int}")]
    [Authorize]
    [ProducesResponseType<SupportTicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportTicketDetailDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _support.GetMineAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>A reply of the customer; 409 on a resolved request.</summary>
    [HttpPost("tickets/{id:int}/replies")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Support)]
    [ProducesResponseType<SupportTicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupportTicketDetailDto>> Reply(int id, SupportReplyRequestDto request, CancellationToken cancellationToken)
        => Ok(await _support.ReplyAsync(User.GetUserId(), id, request, cancellationToken));
}
