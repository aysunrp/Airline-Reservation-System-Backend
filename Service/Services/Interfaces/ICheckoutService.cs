using Service.Helpers.DTOs.Payment;

namespace Service.Services.Interfaces;

/// <summary>Stripe Checkout for bookings. Opening the page never confirms anything; only the Stripe webhook does.</summary>
public interface ICheckoutService
{
    /// <summary>
    /// Opens (or offers again) the Stripe page for a pending booking, charging the booking's own total, and keeps its seats held
    /// until that page closes. 404 when the caller may not pay this booking, 409 when it is paid, 422 when it can no longer be paid,
    /// 503 when Stripe is not configured or not reachable.
    /// </summary>
    Task<CheckoutSessionDto> CreateSessionAsync(int? userId, CreateCheckoutSessionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The state of one payment, for the booking's account holder, an Admin or SuperAdmin, or whoever holds the Stripe session id
    /// from the return address. Anybody else is told it does not exist.
    /// </summary>
    Task<PaymentStatusDto> GetStatusAsync(int paymentId, int? userId, bool isAdmin, string? checkoutSessionId, CancellationToken cancellationToken = default);
}
