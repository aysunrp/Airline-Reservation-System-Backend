using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Flight;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Seat;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Flights" and "Seat Inventory" pages.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/flights")]
[Produces("application/json")]
public class AdminFlightsController : ControllerBase
{
    private readonly IAdminFlightService _flightService;
    private readonly IFlightSearchService _flightSearchService;
    private readonly IAuditLogService _audit;

    public AdminFlightsController(IAdminFlightService flightService, IFlightSearchService flightSearchService, IAuditLogService audit)
    {
        _flightService = flightService;
        _flightSearchService = flightSearchService;
        _audit = audit;
    }

    /// <summary>
    /// A page of flights, oldest departure first. Filtered by flight number, city or aircraft model, by status and by the UTC
    /// departure day. <c>take</c> is 1-500 (100 by default); use <c>skip</c> to read further.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminFlightRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminFlightRowDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] FlightStatus? status, [FromQuery] DateOnly? date,
        [FromQuery] string? origin, [FromQuery] string? destination, [FromQuery] int? month, [FromQuery] int? year,
        [FromQuery] int? aircraftId, [FromQuery] string? flightType, [FromQuery] bool? active,
        [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        => Ok(await _flightService.GetFilteredPageAsync(
            new AdminFlightFilter(search, origin, destination, status, date, month, year, aircraftId, flightType, active),
            skip, take, cancellationToken));

    /// <summary>The journeys with a transfer between two airports over up to 62 departure days (no seat filter).</summary>
    [HttpGet("journeys")]
    [ProducesResponseType<IReadOnlyList<AdminJourneyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<AdminJourneyDto>>> GetJourneys(
        [FromQuery] string origin, [FromQuery] string destination, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken = default)
        => Ok(await _flightService.GetJourneysAsync(origin, destination, from, to, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<FlightResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlightResultDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _flightSearchService.GetFlightAsync(id, cancellationToken));

    /// <summary>Creates the flight with its fares and a seat inventory built from the aircraft's seats.</summary>
    [HttpPost]
    [ProducesResponseType<FlightResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FlightResultDto>> Create(FlightRequestDto request, CancellationToken cancellationToken)
    {
        var flight = await _flightService.CreateAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FlightCreated, "Flight", flight.Id.ToString(), cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = flight.Id }, flight);
    }

    /// <summary>Status or time changes are announced to passengers; route and aircraft are fixed once booked (422).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<FlightResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FlightResultDto>> Update(int id, FlightRequestDto request, CancellationToken cancellationToken)
    {
        var flight = await _flightService.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FlightUpdated, "Flight", id.ToString(), cancellationToken: cancellationToken);
        return Ok(flight);
    }

    /// <summary>Only flights without bookings can be deleted; booked flights should be cancelled (422).</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _flightService.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FlightDeleted, "Flight", id.ToString(), cancellationToken: cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/seats")]
    [ProducesResponseType<SeatInventoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeatInventoryDto>> GetSeatInventory(int id, [FromQuery] CabinClass? cabinClass, CancellationToken cancellationToken)
        => Ok(await _flightService.GetSeatInventoryAsync(id, cabinClass, cancellationToken));

    /// <summary>Blocks a free seat or opens a blocked one; reserved or occupied seats cannot be blocked (422).</summary>
    [HttpPatch("{id:int}/seats/{seatId:int}")]
    [ProducesResponseType<InventorySeatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InventorySeatDto>> SetSeatBlocked(int id, int seatId, SeatBlockRequestDto request, CancellationToken cancellationToken)
    {
        var seat = await _flightService.SetSeatBlockedAsync(id, seatId, request.Blocked, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.SeatChanged, "Flight", id.ToString(), $"Seat {seatId}: {(request.Blocked ? "blocked" : "opened")}", cancellationToken);
        return Ok(seat);
    }
}
