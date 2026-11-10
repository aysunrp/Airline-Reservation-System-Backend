using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// Admin management of the baggage and meal options offered on the booking pages. Codes are unique (409);
/// options already sold with tickets cannot be deleted and should be set inactive instead (422).
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/booking-options")]
[Produces("application/json")]
public class AdminBookingOptionsController : ControllerBase
{
    private readonly IAdminBookingOptionService _optionService;

    public AdminBookingOptionsController(IAdminBookingOptionService optionService)
    {
        _optionService = optionService;
    }

    /// <summary>All baggage options, including inactive ones.</summary>
    [HttpGet("baggage")]
    [ProducesResponseType<IReadOnlyList<BaggageOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BaggageOptionDto>>> GetBaggage(CancellationToken cancellationToken)
        => Ok(await _optionService.GetBaggageOptionsAsync(cancellationToken));

    [HttpGet("baggage/{id:int}")]
    [ProducesResponseType<BaggageOptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaggageOptionDto>> GetBaggageOption(int id, CancellationToken cancellationToken)
        => Ok(await _optionService.GetBaggageOptionAsync(id, cancellationToken));

    [HttpPost("baggage")]
    [ProducesResponseType<BaggageOptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaggageOptionDto>> CreateBaggageOption(BaggageOptionRequestDto request, CancellationToken cancellationToken)
    {
        var option = await _optionService.CreateBaggageOptionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBaggageOption), new { id = option.Id }, option);
    }

    /// <summary>Tickets already sold keep the price they were charged.</summary>
    [HttpPut("baggage/{id:int}")]
    [ProducesResponseType<BaggageOptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaggageOptionDto>> UpdateBaggageOption(int id, BaggageOptionRequestDto request, CancellationToken cancellationToken)
        => Ok(await _optionService.UpdateBaggageOptionAsync(id, request, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("baggage/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteBaggageOption(int id, CancellationToken cancellationToken)
    {
        await _optionService.DeleteBaggageOptionAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>All meal options, including inactive ones.</summary>
    [HttpGet("meals")]
    [ProducesResponseType<IReadOnlyList<MealOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MealOptionDto>>> GetMeals(CancellationToken cancellationToken)
        => Ok(await _optionService.GetMealOptionsAsync(cancellationToken));

    [HttpGet("meals/{id:int}")]
    [ProducesResponseType<MealOptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealOptionDto>> GetMealOption(int id, CancellationToken cancellationToken)
        => Ok(await _optionService.GetMealOptionAsync(id, cancellationToken));

    [HttpPost("meals")]
    [ProducesResponseType<MealOptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOptionDto>> CreateMealOption(MealOptionRequestDto request, CancellationToken cancellationToken)
    {
        var option = await _optionService.CreateMealOptionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMealOption), new { id = option.Id }, option);
    }

    [HttpPut("meals/{id:int}")]
    [ProducesResponseType<MealOptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOptionDto>> UpdateMealOption(int id, MealOptionRequestDto request, CancellationToken cancellationToken)
        => Ok(await _optionService.UpdateMealOptionAsync(id, request, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("meals/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteMealOption(int id, CancellationToken cancellationToken)
    {
        await _optionService.DeleteMealOptionAsync(id, cancellationToken);
        return NoContent();
    }
}
