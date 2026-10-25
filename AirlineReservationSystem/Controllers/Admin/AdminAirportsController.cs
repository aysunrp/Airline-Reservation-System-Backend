using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Airport;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin management of airports: codes, names and the time zones schedules are read in.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/airports")]
[Produces("application/json")]
public class AdminAirportsController : ControllerBase
{
    private readonly IAdminAirportService _airportService;

    public AdminAirportsController(IAdminAirportService airportService)
    {
        _airportService = airportService;
    }

    /// <summary>All airports, including inactive ones; filtered by code, name, city or country and by status.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AirportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AirportDto>>> GetAll([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
        => Ok(await _airportService.GetAllAsync(search, isActive, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AirportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AirportDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _airportService.GetAsync(id, cancellationToken));

    /// <summary>The code must be unique (409) and the time zone an IANA name such as Asia/Baku (400).</summary>
    [HttpPost]
    [ProducesResponseType<AirportDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AirportDto>> Create(AirportRequestDto request, CancellationToken cancellationToken)
    {
        var airport = await _airportService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = airport.Id }, airport);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<AirportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AirportDto>> Update(int id, AirportRequestDto request, CancellationToken cancellationToken)
        => Ok(await _airportService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Airports used by routes, schedules or flights cannot be deleted; set them inactive instead (422).</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _airportService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
