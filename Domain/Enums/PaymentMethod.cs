namespace Domain.Enums;

public enum PaymentMethod
{
    Card,
    ApplePay,
    GooglePay,

    /// <summary>Paid on Stripe's Checkout page; Stripe decides which card or wallet was used.</summary>
    StripeCheckout
}
