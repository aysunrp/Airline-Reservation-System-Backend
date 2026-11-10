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
    private readonly IPricingService _pricingService;

    public BookingOptionsController(IExtraServiceOptionsService optionsService, IPricingService pricingService)
    {
        _optionsService = optionsService;
        _pricingService = pricingService;
    }

    /// <summary>The age limits (infant, child, adult) and the passenger limit the server applies to a booking.</summary>
    [HttpGet("passenger-rules")]
    [ProducesResponseType<PassengerRulesDto>(StatusCodes.Status200OK)]
    public ActionResult<PassengerRulesDto> GetPassengerRules()
        => Ok(_pricingService.GetPassengerRules());

    [HttpGet("baggage")]
    [ProducesResponseType<IReadOnlyList<BaggageOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BaggageOptionDto>>> GetBaggage(CancellationToken cancellationToken)
        => Ok(await _optionsService.GetBaggageOptionsAsync(cancellationToken));

    [HttpGet("meals")]
    [ProducesResponseType<IReadOnlyList<MealOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MealOptionDto>>> GetMeals(CancellationToken cancellationToken)
        => Ok(await _optionsService.GetMealOptionsAsync(cancellationToken));
}
