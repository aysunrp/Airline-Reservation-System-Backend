using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seat;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Flight results, flight details and seat selection pages.</summary>
[ApiController]
[Route("api/flights")]
[Produces("application/json")]
public class FlightsController : ControllerBase
{
    private readonly IFlightSearchService _flightSearchService;
    private readonly ISeatService _seatService;

    public FlightsController(IFlightSearchService flightSearchService, ISeatService seatService)
    {
        _flightSearchService = flightSearchService;
        _seatService = seatService;
    }

    /// <summary>One-way, round-trip or multi-city search: one result list per leg.</summary>
    [HttpPost("search")]
    [ProducesResponseType<IReadOnlyList<FlightSearchLegResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FlightSearchLegResultDto>>> Search(FlightSearchRequestDto request, CancellationToken cancellationToken)
        => Ok(await _flightSearchService.SearchAsync(request, cancellationToken));

    /// <summary>
    /// Price calendar: the cheapest bookable fare for each departure day (airport time) of one route, for at most 62 days.
    /// Days without a bookable flight are not listed.
    /// </summary>
    [HttpGet("lowest-fares")]
    [ProducesResponseType<IReadOnlyList<DayFareDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DayFareDto>>> GetLowestFares([FromQuery] LowestFaresRequestDto request, CancellationToken cancellationToken)
        => Ok(await _flightSearchService.GetLowestFaresAsync(request, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<FlightResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlightResultDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _flightSearchService.GetFlightAsync(id, cancellationToken));

    /// <summary>Seat map with live availability, optionally for one cabin.</summary>
    [HttpGet("{id:int}/seats")]
    [ProducesResponseType<SeatMapDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeatMapDto>> GetSeatMap(int id, [FromQuery] CabinClass? cabinClass, CancellationToken cancellationToken)
        => Ok(await _seatService.GetSeatMapAsync(id, cabinClass, cancellationToken));
}
