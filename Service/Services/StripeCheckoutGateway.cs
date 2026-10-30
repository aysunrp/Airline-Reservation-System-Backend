using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace Service.Services;

/// <summary>
/// Stripe Checkout through the official SDK. Card details never reach AEROVA: the customer types them on Stripe's page.
/// Only the kind of a Stripe error is logged, never the key, the customer or the amount.
/// </summary>
public class StripeCheckoutGateway : IStripeCheckoutGateway
{
    private const string UnavailableMessage = "Kartla ödəniş hazırda əlçatan deyil. Bir az sonra yenidən cəhd edin.";

    private readonly StripeSettings _settings;
    private readonly ILogger<StripeCheckoutGateway> _logger;

    public StripeCheckoutGateway(IOptions<StripeSettings> settings, ILogger<StripeCheckoutGateway> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<StripeCheckoutSession> CreateSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.PaymentId.ToString(),
            CustomerEmail = request.CustomerEmail,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ExpiresAt = request.ExpiresAtUtc,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = request.Description }
                    }
                }
            },
            Metadata = Metadata(request),
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = Metadata(request) }
        };

        try
        {
            // The same payment never opens two pages, even when the request is retried.
            var session = await Sessions().CreateAsync(options, new RequestOptions { IdempotencyKey = $"aerova-checkout-{request.PaymentId}" }, cancellationToken);
            return new StripeCheckoutSession(session.Id, session.Url, session.ExpiresAt.ToUniversalTime());
        }
        catch (StripeException exception)
        {
            _logger.LogWarning("Stripe refused to create a Checkout Session (HTTP {Status}, {ErrorType}, {ErrorCode}): {Reason}",
                (int?)exception.HttpStatusCode, exception.StripeError?.Type, exception.StripeError?.Code, exception.StripeError?.Message ?? exception.Message);
            throw new ServiceUnavailableException(UnavailableMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Stripe could not be reached to create a Checkout Session ({ExceptionType}).", exception.GetType().Name);
            throw new ServiceUnavailableException(UnavailableMessage);
        }
    }

    public async Task ExpireSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await Sessions().ExpireAsync(sessionId, new SessionExpireOptions(), null, cancellationToken);
        }
        catch (StripeException exception) when (exception.StripeError?.Code is "resource_missing" or "checkout_session_already_expired" || exception.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Already closed, paid or unknown: nothing left to close.
            _logger.LogInformation("A Checkout Session could not be closed ({ErrorCode}); it is no longer open.", exception.StripeError?.Code);
        }
    }

    private SessionService Sessions()
    {
        if (!_settings.IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        return new SessionService(new StripeClient(_settings.SecretKey));
    }

    private static Dictionary<string, string> Metadata(StripeCheckoutRequest request) => new()
    {
        ["payment_id"] = request.PaymentId.ToString(),
        ["booking_id"] = request.BookingId.ToString(),
        ["pnr"] = request.Pnr
    };
}
