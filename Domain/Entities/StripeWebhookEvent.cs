using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A Stripe event that reached the webhook. The unique <see cref="EventId"/> makes a repeated delivery harmless: an event that was
/// handled once is acknowledged again without doing anything. Only identifiers are kept, never the payload.
/// </summary>
public class StripeWebhookEvent : BaseEntity
{
    /// <summary>Stripe's id of the event (evt_...).</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>e.g. checkout.session.completed.</summary>
    public string Type { get; set; } = string.Empty;

    public string? CheckoutSessionId { get; set; }

    /// <summary>Null until the event was handled; an event that failed is retried by Stripe and handled then.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>What the event led to, e.g. "Confirmed" or "Ignored: amount mismatch".</summary>
    public string? Outcome { get; set; }
}
