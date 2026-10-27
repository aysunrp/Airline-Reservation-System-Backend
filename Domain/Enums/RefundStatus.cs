namespace Domain.Enums;

public enum RefundStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    NotEligible,

    /// <summary>Stripe cancelled the refund before it reached the customer's bank (for example a bank transfer that was never completed).</summary>
    Cancelled
}
