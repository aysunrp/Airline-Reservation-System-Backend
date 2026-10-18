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
}
