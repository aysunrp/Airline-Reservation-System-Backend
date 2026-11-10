using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Refund : BaseEntity
{
    public int CancellationId { get; set; }
    public int PaymentId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO code in capitals; it is the currency of the payment that is refunded.</summary>
    public string Currency { get; set; } = Constants.AppConstants.Currency;

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    /// <summary>The Stripe Refund (re_...) once Stripe has accepted the request; null before that and for the pretend provider's own reference.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>The status Stripe last reported (pending, requires_action, succeeded, failed, canceled); kept next to the daily status for support.</summary>
    public string? ProviderStatus { get; set; }

    /// <summary>The cancellation reason the customer or the admin gave.</summary>
    public string? Reason { get; set; }

    /// <summary>Why the refund did not go through, in words that are safe to show to staff.</summary>
    public string? FailureReason { get; set; }

    /// <summary>How many times the refund was sent to Stripe as a new request; it is part of Stripe's idempotency key, so a retry after a failure is a new refund and a repeated call is not.</summary>
    public int Attempts { get; set; } = 1;

    /// <summary>The signed-in customer or admin who cancelled the booking; null for a guest.</summary>
    public int? InitiatedByUserId { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public Cancellation Cancellation { get; set; } = null!;
    public Payment Payment { get; set; } = null!;
    public User? InitiatedByUser { get; set; }
}
