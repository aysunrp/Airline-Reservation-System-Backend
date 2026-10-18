using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Bookings" page: reservation management and cancellations.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/bookings")]
[Produces("application/json")]
public class AdminBookingsController : ControllerBase
{
    private readonly IAdminBookingService _bookingService;

    public AdminBookingsController(IAdminBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>Filtered by PNR fragment, passenger name fragment and status; newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminBookingRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminBookingRowDto>>> GetAll(
        [FromQuery] string? pnr, [FromQuery] string? passenger, [FromQuery] BookingStatus? status, CancellationToken cancellationToken)
        => Ok(await _bookingService.GetAllAsync(pnr, passenger, status, cancellationToken));

    [HttpGet("{pnr:length(6)}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Get(string pnr, CancellationToken cancellationToken)
        => Ok(await _bookingService.GetDetailsAsync(pnr, cancellationToken));

    /// <summary>Same cancellation rules and refund handling as the customer, without the last-name check.</summary>
    [HttpPost("{pnr:length(6)}/cancel")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Cancel(string pnr, AdminCancelBookingRequestDto request, CancellationToken cancellationToken)
        => Ok(await _bookingService.CancelAsync(pnr, request.Reason, cancellationToken));
}
