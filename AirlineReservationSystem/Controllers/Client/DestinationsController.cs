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
}
