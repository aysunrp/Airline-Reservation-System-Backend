namespace Service.Helpers.Settings;

/// <summary>
/// Stripe Checkout. <see cref="SecretKey"/> and <see cref="WebhookSecret"/> belong in user-secrets or environment variables
/// (Stripe__SecretKey, Stripe__WebhookSecret), never in appsettings or the frontend. Use test keys (sk_test_...) in Development.
/// </summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    public string? SecretKey { get; set; }

    /// <summary>The signing secret of the webhook endpoint (whsec_...).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Lower-case ISO code Stripe is asked to charge in; it must match the currency the prices are stored in.</summary>
    public string Currency { get; set; } = "usd";

    /// <summary>How long a Checkout page stays open. Stripe accepts 30 minutes to 24 hours.</summary>
    public int CheckoutMinutes { get; set; } = 31;

    /// <summary>Card payments go through Stripe only when both secrets are set.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
}
