using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>"Apply" button of the promo code field on the payment page.</summary>
[ApiController]
[Route("api/promo-codes")]
[Produces("application/json")]
public class PromoCodesController : ControllerBase
{
    private readonly IPromoCodeService _promoCodeService;

    public PromoCodesController(IPromoCodeService promoCodeService)
    {
        _promoCodeService = promoCodeService;
    }

    /// <summary>Returns the discount when the code can be used now; 422 with the reason otherwise.</summary>
    [HttpGet("{code}")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<PromoCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PromoCodeDto>> Validate(string code, CancellationToken cancellationToken)
        => Ok(await _promoCodeService.ValidateAsync(code, cancellationToken));
}
