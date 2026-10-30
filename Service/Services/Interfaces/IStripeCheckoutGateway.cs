namespace Service.Services.Interfaces;

/// <summary>What AEROVA asks Stripe for one Checkout page: one line, the booking's total in minor units (cents for USD).</summary>
public sealed record StripeCheckoutRequest(
    int PaymentId,
    int BookingId,
    string Pnr,
    long AmountMinor,
    string Currency,
    string Description,
    string? CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    DateTime ExpiresAtUtc);

public sealed record StripeCheckoutSession(string Id, string Url, DateTime ExpiresAtUtc);

/// <summary>The only place that talks to the Stripe API, so the rest can be tested without it.</summary>
public interface IStripeCheckoutGateway
{
    /// <summary>Creates the Checkout Session. Throws <see cref="Helpers.Exceptions.ServiceUnavailableException"/> when Stripe cannot be reached or refuses.</summary>
    Task<StripeCheckoutSession> CreateSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>Closes an open Checkout page so it can no longer be paid. A page that is already closed is fine.</summary>
    Task ExpireSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Reads a Checkout page from Stripe with the secret key. Null when Stripe cannot be reached or does not know the page.</summary>
    Task<Stripe.Checkout.Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
