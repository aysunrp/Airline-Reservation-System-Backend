using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Choices on the baggage and meal selection pages.</summary>
[ApiController]
[Route("api/booking-options")]
[Produces("application/json")]
public class BookingOptionsController : ControllerBase
{
    private readonly IExtraServiceOptionsService _optionsService;

    public BookingOptionsController(IExtraServiceOptionsService optionsService)
    {
        _optionsService = optionsService;
    }

    [HttpGet("baggage")]
    [ProducesResponseType<IReadOnlyList<BaggageOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BaggageOptionDto>>> GetBaggage(CancellationToken cancellationToken)
        => Ok(await _optionsService.GetBaggageOptionsAsync(cancellationToken));

    [HttpGet("meals")]
    [ProducesResponseType<IReadOnlyList<MealOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MealOptionDto>>> GetMeals(CancellationToken cancellationToken)
        => Ok(await _optionsService.GetMealOptionsAsync(cancellationToken));
}
