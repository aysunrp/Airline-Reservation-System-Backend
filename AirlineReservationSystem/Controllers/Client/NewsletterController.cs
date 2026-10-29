using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Newsletter;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Newsletter subscription of the home page: subscribe, confirm by e-mail, unsubscribe. Open to visitors; no account is needed.</summary>
[ApiController]
[Route("api/newsletter")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Newsletter)]
public class NewsletterController : ControllerBase
{
    private readonly INewsletterService _newsletter;

    public NewsletterController(INewsletterService newsletter)
    {
        _newsletter = newsletter;
    }

    /// <summary>
    /// Asks the owner of the address to confirm by e-mail. The answer is the same whether the address is new, waiting, already subscribed or
    /// was unsubscribed (nobody can find out who is on the list); a second request within a few minutes sends no second e-mail. 503 when the
    /// e-mail cannot be sent. The subscription starts only when the link in the e-mail is opened.
    /// </summary>
    [HttpPost("subscribe")]
    [ProducesResponseType<NewsletterSubscribeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<NewsletterSubscribeResponseDto>> Subscribe(NewsletterSubscribeRequestDto request, CancellationToken cancellationToken)
        => Ok(await _newsletter.SubscribeAsync(request, cancellationToken));

    /// <summary>Activates the subscription of the link in the confirmation e-mail. The link works once; 422 with code link_invalid or link_expired otherwise.</summary>
    [HttpPost("confirm")]
    [ProducesResponseType<NewsletterResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<NewsletterResultDto>> Confirm(NewsletterTokenRequestDto request, CancellationToken cancellationToken)
        => Ok(await _newsletter.ConfirmAsync(request, cancellationToken));

    /// <summary>Opts out with the link of any newsletter e-mail; no account needed. Safe to repeat.</summary>
    [HttpPost("unsubscribe")]
    [ProducesResponseType<NewsletterResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<NewsletterResultDto>> Unsubscribe(NewsletterTokenRequestDto request, CancellationToken cancellationToken)
        => Ok(await _newsletter.UnsubscribeAsync(request, cancellationToken));
}
