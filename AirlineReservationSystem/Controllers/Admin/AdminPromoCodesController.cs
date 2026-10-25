using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin management of promo codes: discount, validity window, usage limit and status.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/promo-codes")]
[Produces("application/json")]
public class AdminPromoCodesController : ControllerBase
{
    private readonly IAdminPromoCodeService _promoCodeService;

    public AdminPromoCodesController(IAdminPromoCodeService promoCodeService)
    {
        _promoCodeService = promoCodeService;
    }

    /// <summary>All promo codes with their usage, filtered by code fragment and status.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminPromoCodeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPromoCodeDto>>> GetAll([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
        => Ok(await _promoCodeService.GetAllAsync(search, isActive, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminPromoCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromoCodeDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _promoCodeService.GetAsync(id, cancellationToken));

    /// <summary>Codes are unique regardless of case (409).</summary>
    [HttpPost]
    [ProducesResponseType<AdminPromoCodeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromoCodeDto>> Create(PromoCodeRequestDto request, CancellationToken cancellationToken)
    {
        var promoCode = await _promoCodeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = promoCode.Id }, promoCode);
    }

    /// <summary>The usage limit cannot go below the number of times the code was already used (422).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AdminPromoCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPromoCodeDto>> Update(int id, PromoCodeRequestDto request, CancellationToken cancellationToken)
        => Ok(await _promoCodeService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Codes used by bookings cannot be deleted; set them inactive instead (422).</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _promoCodeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
