using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;
using Stripe;

namespace Service.Services;

/// <summary>
/// Stripe Refunds through the official SDK. The same refund and attempt always carry the same idempotency key, so a repeated
/// call returns the refund Stripe already made instead of making a second one. Only the kind of a Stripe error is logged.
/// </summary>
public class StripeRefundGateway : IStripeRefundGateway
{
    private const string UnavailableMessage = "Refunds are not available right now. Please try again later.";

    private readonly StripeSettings _settings;
    private readonly ILogger<StripeRefundGateway> _logger;

    public StripeRefundGateway(IOptions<StripeSettings> settings, ILogger<StripeRefundGateway> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<StripeRefundResult> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var options = new RefundCreateOptions
        {
            PaymentIntent = request.PaymentIntentId,
            Amount = request.AmountMinor,
            Reason = "requested_by_customer",
            Metadata = new Dictionary<string, string>
            {
                ["refund_id"] = request.RefundId.ToString(),
                ["pnr"] = request.Pnr
            }
        };

        try
        {
            var refund = await new Stripe.RefundService(new StripeClient(_settings.SecretKey)).CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = $"aerova-refund-{request.RefundId}-{request.Attempt}" },
                cancellationToken);

            return new StripeRefundResult(refund.Id, refund.Status, refund.FailureReason);
        }
        catch (StripeException exception) when (IsPermanent(exception))
        {
            // For example charge_already_refunded or an amount above what is left: asking again changes nothing.
            var reason = exception.StripeError?.Code ?? exception.StripeError?.Type ?? "refused";
            _logger.LogWarning("Stripe refused a refund (HTTP {Status}, {ErrorCode}).", (int?)exception.HttpStatusCode, reason);
            return new StripeRefundResult(null, "failed", reason);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning("Stripe could not create a refund now (HTTP {Status}, {ErrorCode}).", (int?)exception.HttpStatusCode, exception.StripeError?.Code);
            throw new ServiceUnavailableException(UnavailableMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Stripe could not be reached to create a refund ({ExceptionType}).", exception.GetType().Name);
            throw new ServiceUnavailableException(UnavailableMessage);
        }
    }

    // A 4xx answer is a decision about this request; 409 (the idempotent call is still running), 429 and 5xx are worth trying again.
    // A rejected key (401) is a setup problem: it stays queued so it can be sent once the key is right.
    private static bool IsPermanent(StripeException exception)
        => exception.HttpStatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.PaymentRequired or System.Net.HttpStatusCode.NotFound;
}
