using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Payment : BaseEntity
{
    public int BookingId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>ISO code in capitals, e.g. USD.</summary>
    public string Currency { get; set; } = Constants.AppConstants.Currency;

    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ProviderReference { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardBrand { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>The Stripe Checkout Session (cs_...) this payment is made through; the webhook finds the payment by it.</summary>
    public string? StripeCheckoutSessionId { get; set; }

    /// <summary>The Stripe PaymentIntent (pi_...) once Stripe has taken the money; refunds go through it.</summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>The Stripe page the customer pays on, kept so an open page is offered again instead of opening a second one.</summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>When the Stripe page closes; after that it can no longer be paid.</summary>
    public DateTime? CheckoutExpiresAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
