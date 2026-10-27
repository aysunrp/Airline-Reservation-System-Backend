namespace Service.Services.Interfaces;

public enum StripeWebhookResult
{
    /// <summary>The event was checked and recorded (whatever it led to).</summary>
    Handled,

    /// <summary>The event was handled before; nothing was done again.</summary>
    Duplicate,

    /// <summary>The signature did not prove the call came from Stripe; nothing was read or changed.</summary>
    InvalidSignature
}

/// <summary>
/// Stripe's calls about Checkout pages. This is the only place a Stripe payment confirms a booking: never the browser, never
/// the return page. An error is thrown, and Stripe retries, when an event could not be handled.
/// </summary>
public interface IStripeWebhookService
{
    Task<StripeWebhookResult> HandleAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default);
}
