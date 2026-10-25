using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Aircraft;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Aircraft" page; seats are generated from the cabin seat counts.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/aircraft")]
[Produces("application/json")]
public class AdminAircraftController : ControllerBase
{
    private readonly IAdminAircraftService _aircraftService;

    public AdminAircraftController(IAdminAircraftService aircraftService)
    {
        _aircraftService = aircraftService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AircraftDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AircraftDto>>> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await _aircraftService.GetAllAsync(search, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AircraftDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AircraftDetailsDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _aircraftService.GetDetailsAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AircraftDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AircraftDetailsDto>> Create(AircraftRequestDto request, CancellationToken cancellationToken)
    {
        var aircraft = await _aircraftService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = aircraft.Aircraft.Id }, aircraft);
    }

    /// <summary>The seat layout can only change while no flight or schedule uses the aircraft (422 otherwise).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AircraftDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AircraftDetailsDto>> Update(int id, AircraftRequestDto request, CancellationToken cancellationToken)
        => Ok(await _aircraftService.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _aircraftService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
