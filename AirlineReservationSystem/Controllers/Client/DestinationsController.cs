using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Destinations;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>The destination catalog behind Explore Destinations, the search form and the voice search.</summary>
[ApiController]
[Route("api/destinations")]
[Produces("application/json")]
public class DestinationsController : ControllerBase
{
    private readonly IDestinationCatalogService _catalog;

    public DestinationsController(IDestinationCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// Active countries with their active cities and airports, in catalog order. An airport with <c>hasRoutes</c> false has no active
    /// route yet: it can be shown, but nothing can be searched or booked there.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DestinationCountryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DestinationCountryDto>>> Get(CancellationToken cancellationToken)
        => Ok(await _catalog.GetPublicAsync(cancellationToken));

    /// <summary>
    /// One city's destination page by its database id: the city with its own airports, its country and the other cities of that country.
    /// The home base (Baku), a switched-off city and an unknown id answer 404.
    /// </summary>
    [HttpGet("cities/{id:int}")]
    [ProducesResponseType<CityDestinationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CityDestinationDto>> GetCity(int id, CancellationToken cancellationToken)
        => Ok(await _catalog.GetCityAsync(id, cancellationToken));

    /// <summary>The upcoming flights from the home base to the airports of the city. Real flights only: an empty list means none is scheduled.</summary>
    [HttpGet("cities/{id:int}/flights")]
    [ProducesResponseType<IReadOnlyList<DestinationFlightDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DestinationFlightDto>>> GetCityFlights(int id, CancellationToken cancellationToken)
        => Ok(await _catalog.GetCityFlightsAsync(id, cancellationToken));
}
