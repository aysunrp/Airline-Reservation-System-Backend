using Domain.Enums;

namespace Service.Services;

/// <summary>The one place that turns a Stripe refund status into AEROVA's, so the cancel flow and the webhook agree.</summary>
public static class StripeRefundStatusMapper
{
    /// <summary>
    /// succeeded is the only status that means the money is on its way back for good (Completed). pending and requires_action
    /// mean Stripe took the request but the refund is not finished (Processing). failed and canceled are final (Failed, Cancelled).
    /// Anything else is treated as not finished, never as paid back.
    /// </summary>
    public static RefundStatus ToRefundStatus(string? stripeStatus) => stripeStatus switch
    {
        "succeeded" => RefundStatus.Completed,
        "failed" => RefundStatus.Failed,
        "canceled" => RefundStatus.Cancelled,
        _ => RefundStatus.Processing
    };
}
