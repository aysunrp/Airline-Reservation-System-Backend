using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Destinations;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// The destination catalog in the admin panel. Countries and cities are switched on and off, never deleted: a switched-off country or city
/// leaves the public catalog while its airports, routes, flights and bookings stay as they are. Airports are managed under api/admin/airports.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/destinations")]
[Produces("application/json")]
public class AdminDestinationsController : ControllerBase
{
    private readonly IDestinationCatalogService _catalog;

    public AdminDestinationsController(IDestinationCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DestinationCountryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DestinationCountryDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _catalog.GetAllAsync(cancellationToken));

    /// <summary>Edits a country's names and descriptions (400 for invalid text); everything that shows the destination reads the same record.</summary>
    [HttpPut("countries/{id:int}")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DestinationCountryDto>> UpdateCountry(int id, [FromBody] DestinationCountryRequestDto request, CancellationToken cancellationToken)
        => Ok(await _catalog.UpdateCountryAsync(id, request, cancellationToken));

    /// <summary>Edits a city's Azerbaijani and Russian names and its extra spellings.</summary>
    [HttpPut("cities/{id:int}")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DestinationCountryDto>> UpdateCity(int id, [FromBody] DestinationCityRequestDto request, CancellationToken cancellationToken)
        => Ok(await _catalog.UpdateCityAsync(id, request, cancellationToken));

    [HttpPut("countries/{id:int}/active")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DestinationCountryDto>> SetCountryActive(int id, [FromBody] SetActiveRequestDto request, CancellationToken cancellationToken)
        => Ok(await _catalog.SetCountryActiveAsync(id, request.IsActive, cancellationToken));

    [HttpPut("cities/{id:int}/active")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DestinationCountryDto>> SetCityActive(int id, [FromBody] SetActiveRequestDto request, CancellationToken cancellationToken)
        => Ok(await _catalog.SetCityActiveAsync(id, request.IsActive, cancellationToken));
}
