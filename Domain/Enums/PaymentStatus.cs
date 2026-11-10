namespace Domain.Enums;

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,

    /// <summary>The Stripe page closed (or the booking ran out) without a payment.</summary>
    Cancelled
}
