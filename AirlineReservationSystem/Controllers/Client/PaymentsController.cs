using System.Text;
using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Payment page: Stripe Checkout, and the payment status the return page asks for.</summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICheckoutService _checkoutService;
    private readonly IStripeWebhookService _webhookService;

    public PaymentsController(IPaymentService paymentService, ICheckoutService checkoutService, IStripeWebhookService webhookService)
    {
        _paymentService = paymentService;
        _checkoutService = checkoutService;
        _webhookService = webhookService;
    }

    /// <summary>
    /// The old pretend payment with card details, for local demos and tests only (Payments:AllowMockPayments). Refused (422) once
    /// Stripe is configured, and 503 where it is not allowed.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<BookingDto>> Pay(PaymentRequestDto request, CancellationToken cancellationToken)
        => Ok(await _paymentService.PayAsync(request, cancellationToken));

    /// <summary>
    /// Opens Stripe's payment page for a pending booking and returns its address. The amount is the booking's own total; nothing
    /// sent here can change it. A booking made while signed in needs that account's token, a guest booking the last name of a passenger.
    /// Opening the page confirms nothing: only Stripe's webhook does.
    /// </summary>
    [HttpPost("checkout-session")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<CheckoutSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CheckoutSessionDto>> CreateCheckoutSession(CreateCheckoutSessionRequestDto request, CancellationToken cancellationToken)
        => Ok(await _checkoutService.CreateSessionAsync(User.GetUserIdOrNull(), request, cancellationToken));

    /// <summary>
    /// The status of one payment. Readable by the booking's account holder, an Admin or SuperAdmin, or with the Stripe session id from
    /// the return address (<c>session</c>); anybody else gets 404. It only reports; it never marks a payment as paid.
    /// </summary>
    [HttpGet("{paymentId:int}")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<PaymentStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentStatusDto>> GetStatus(int paymentId, [FromQuery] string? session, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.SuperAdmin);
        return Ok(await _checkoutService.GetStatusAsync(paymentId, User.GetUserIdOrNull(), isAdmin, session, cancellationToken));
    }

    /// <summary>
    /// Stripe's events about Checkout pages. No user token: the Stripe-Signature header, checked against the raw body with the
    /// webhook secret, proves the call is Stripe's (400 when it does not). 200 once the event is handled or was handled before;
    /// any other answer makes Stripe send it again later.
    /// </summary>
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes Stripe sent, so the body is read as it is, not bound to a model.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var result = await _webhookService.HandleAsync(payload, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return result == StripeWebhookResult.InvalidSignature
            ? Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid signature", detail: "The Stripe signature of this call is not valid.")
            : Ok();
    }
}
