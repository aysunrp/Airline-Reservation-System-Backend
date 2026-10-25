using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Booking flow, confirmation, manage booking, My Trips and cancellation.</summary>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICancellationService _cancellationService;

    public BookingsController(IBookingService bookingService, ICancellationService cancellationService)
    {
        _bookingService = bookingService;
        _cancellationService = cancellationService;
    }

    /// <summary>Price summary for the payment page; nothing is held or saved.</summary>
    [HttpPost("quote")]
    [ProducesResponseType<PriceBreakdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PriceBreakdownDto>> Quote(CreateBookingRequestDto request, CancellationToken cancellationToken)
        => Ok(await _bookingService.QuoteAsync(request, cancellationToken));

    /// <summary>
    /// Creates a pending booking and holds the seats until payment. Guests can book;
    /// when a valid token is sent the booking is linked to that account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequestDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await _bookingService.CreateBookingAsync(User.GetUserIdOrNull(), request, cancellationToken));

    /// <summary>My Trips: the signed-in user's bookings, newest first.</summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<BookingSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> GetMine(CancellationToken cancellationToken)
        => Ok(await _bookingService.GetMyBookingsAsync(User.GetUserId(), cancellationToken));

    /// <summary>My Trips details: one of the signed-in user's own bookings by reference.</summary>
    [HttpGet("my/{pnr:length(6)}")]
    [Authorize]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetMyBooking(string pnr, CancellationToken cancellationToken)
        => Ok(await _bookingService.GetMyBookingAsync(User.GetUserId(), pnr, cancellationToken));

    /// <summary>Manage booking: the booking reference plus the last name of one of its passengers.</summary>
    [HttpGet("{pnr:length(6)}")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetByPnr(string pnr, [FromQuery] string lastName, CancellationToken cancellationToken)
        => Ok(await _bookingService.GetByPnrAsync(pnr, lastName, cancellationToken));

    /// <summary>Cancel modal: amount paid, fee and estimated refund under the fare rules.</summary>
    [HttpGet("{pnr:length(6)}/cancellation-quote")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<CancellationQuoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CancellationQuoteDto>> GetCancellationQuote(string pnr, [FromQuery] string lastName, CancellationToken cancellationToken)
        => Ok(await _cancellationService.GetQuoteAsync(pnr, lastName, cancellationToken));

    /// <summary>Cancels the booking for good, releases its seats and requests the refund the fare rules allow.</summary>
    [HttpPost("{pnr:length(6)}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Cancel(string pnr, CancelBookingRequestDto request, CancellationToken cancellationToken)
    {
        // The route identifies the booking; the body carries the last name and the reason.
        request.Pnr = pnr;
        return Ok(await _cancellationService.CancelAsync(request, cancellationToken));
    }
}
