using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Airport;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Origin and destination pickers on the search form.</summary>
[ApiController]
[Route("api/airports")]
[Produces("application/json")]
public class AirportsController : ControllerBase
{
    private readonly IAirportService _airportService;

    public AirportsController(IAirportService airportService)
    {
        _airportService = airportService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AirportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AirportDto>>> GetActive(CancellationToken cancellationToken)
        => Ok(await _airportService.GetActiveAsync(cancellationToken));

    /// <summary>Whether an active route joins two airports (by IATA code, either direction). 422 for equal or malformed codes, 404 for an unknown airport.</summary>
    [HttpGet("route-check")]
    [ProducesResponseType<RouteCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RouteCheckDto>> CheckRoute([FromQuery] string origin, [FromQuery] string destination, CancellationToken cancellationToken)
        => Ok(await _airportService.CheckRouteAsync(origin, destination, cancellationToken));
}
