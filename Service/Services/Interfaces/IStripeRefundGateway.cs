namespace Service.Services.Interfaces;

/// <summary>
/// One refund asked of Stripe: the payment's PaymentIntent, the amount in minor units and the refund's own id and attempt,
/// which together make Stripe's idempotency key, so the same request can be sent again without a second refund.
/// </summary>
public sealed record StripeRefundRequest(
    int RefundId,
    int Attempt,
    string PaymentIntentId,
    long AmountMinor,
    string Currency,
    string Pnr);

/// <summary>What Stripe said: its refund id (null when it refused before creating one), its status and, for a refusal, the reason code.</summary>
public sealed record StripeRefundResult(string? Id, string Status, string? FailureReason);

/// <summary>The only place that creates refunds at Stripe, so the rest can be tested without it.</summary>
public interface IStripeRefundGateway
{
    /// <summary>
    /// Creates the refund. A refusal that will not change (the charge is already refunded, the amount is too high) comes back as a
    /// "failed" result; Stripe being unreachable or busy throws <see cref="Helpers.Exceptions.ServiceUnavailableException"/> and
    /// the refund stays queued.
    /// </summary>
    Task<StripeRefundResult> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default);
}
