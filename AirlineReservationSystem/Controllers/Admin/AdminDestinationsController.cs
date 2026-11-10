using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.DTOs.Media;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// The destination catalog in the admin panel. Countries and cities can be switched off (they leave the public catalog while their airports, routes,
/// flights and bookings stay) or, for a SuperAdmin, deleted when none of their airports is used by a route, a schedule or a flight.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/destinations")]
[Produces("application/json")]
public class AdminDestinationsController : ControllerBase
{
    private readonly IDestinationCatalogService _catalog;
    private readonly IAuditLogService _audit;

    public AdminDestinationsController(IDestinationCatalogService catalog, IAuditLogService audit)
    {
        _catalog = catalog;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DestinationCountryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DestinationCountryDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _catalog.GetAllAsync(cancellationToken));

    /// <summary>Adds a country to the catalog: 201 with the country, 400 for invalid text, 409 when its code already exists or is retired.</summary>
    [HttpPost("countries")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DestinationCountryDto>> CreateCountry([FromBody] DestinationCountryCreateDto request, CancellationToken cancellationToken)
    {
        var created = await _catalog.CreateCountryAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationCountryCreated, "Country", created.Id.ToString(), created.Code, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Adds a city with its first airport to a country: 201 with the country, 404 for an unknown country, 409 for a name or airport that is taken.</summary>
    [HttpPost("countries/{id:int}/cities")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DestinationCountryDto>> CreateCity(int id, [FromBody] DestinationCityCreateDto request, CancellationToken cancellationToken)
    {
        var country = await _catalog.CreateCityAsync(id, request, cancellationToken);
        var city = country.Cities.Single(c => string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationCityCreated, "DestinationCity", city.Id.ToString(), country.Code + "/" + city.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, country);
    }

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

    [HttpDelete("cities/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCity(int id, CancellationToken cancellationToken)
    {
        await _catalog.DeleteCityAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationCityDeleted, "DestinationCity", id.ToString(), null, cancellationToken);
        return NoContent();
    }

    [HttpDelete("countries/{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCountry(int id, CancellationToken cancellationToken)
    {
        await _catalog.DeleteCountryAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationCountryDeleted, "Country", id.ToString(), null, cancellationToken);
        return NoContent();
    }

    [HttpPost("countries/{id:int}/image")]
    [RequestSizeLimit(12_000_000)]
    [ProducesResponseType<MediaUploadResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaUploadResultDto>> UploadCountryImage(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _catalog.ReplaceCountryImageAsync(id, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationImageUpdated, "Country", id.ToString(), result.RelativePath, cancellationToken);
        return Ok(result);
    }

    [HttpPost("cities/{id:int}/image")]
    [RequestSizeLimit(12_000_000)]
    [ProducesResponseType<MediaUploadResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaUploadResultDto>> UploadCityImage(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _catalog.ReplaceCityImageAsync(id, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationImageUpdated, "DestinationCity", id.ToString(), result.RelativePath, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("countries/{id:int}/image")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DestinationCountryDto>> RemoveCountryImage(int id, CancellationToken cancellationToken)
    {
        var updated = await _catalog.RemoveCountryImageAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationImageRemoved, "Country", id.ToString(), null, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("cities/{id:int}/image")]
    [ProducesResponseType<DestinationCountryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DestinationCountryDto>> RemoveCityImage(int id, CancellationToken cancellationToken)
    {
        var updated = await _catalog.RemoveCityImageAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.DestinationImageRemoved, "DestinationCity", id.ToString(), null, cancellationToken);
        return Ok(updated);
    }
}
