using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Account;
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
    private readonly IBookingEmailConfirmationService _confirmation;

    public BookingsController(IBookingService bookingService, ICancellationService cancellationService, IBookingEmailConfirmationService confirmation)
    {
        _confirmation = confirmation;
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
    /// Creates a pending booking and holds the seats until payment. Only a signed-in account can book: without a valid token the
    /// answer is 401 and nothing is held. The booking belongs to that account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequestDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await _bookingService.CreateBookingAsync(User.GetUserIdOrNull(), request, cancellationToken));

    /// <summary>
    /// Confirms the booking behind the token of the e-mailed link (no sign-in needed: the link itself is the proof). The token works once;
    /// a wrong, used or expired one gives 422 with a code (link_invalid, link_used, link_expired, link_confirmed).
    /// </summary>
    [HttpPost("confirm-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConfirmEmail(VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        await _confirmation.ConfirmAsync(request.Token, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends a new confirmation link for the signed-in account's own booking (one per minute); the older link stops working.</summary>
    [HttpPost("{pnr:length(6)}/resend-confirmation")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResendConfirmation(string pnr, ResendVerificationRequestDto request, CancellationToken cancellationToken)
    {
        await _confirmation.ResendAsync(User.GetUserIdOrNull() ?? 0, pnr, request.Language, cancellationToken);
        return Accepted();
    }

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
        => Ok(await _cancellationService.GetQuoteAsync(pnr, lastName, User.GetUserIdOrNull(), cancellationToken));

    /// <summary>
    /// Cancels the booking for good, releases its seats and requests the refund the fare rules allow. A booking made under an account
    /// needs that account's token (401 without one); a guest booking needs the last name of a passenger.
    /// </summary>
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
        return Ok(await _cancellationService.CancelAsync(request, User.GetUserIdOrNull(), cancellationToken));
    }
}
