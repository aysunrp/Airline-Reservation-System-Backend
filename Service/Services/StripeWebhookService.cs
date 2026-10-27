using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace Service.Services;

/// <summary>
/// Handles Stripe's events about Checkout pages:
/// <list type="bullet">
/// <item>the signature is checked first (nothing is read or changed without it);</item>
/// <item>each event id is handled once; the same event again is acknowledged and ignored;</item>
/// <item>a payment is matched by its Checkout Session and must carry its own payment and booking ids, amount and currency;</item>
/// <item>the event record, the payment and the booking are saved together, so a failure leaves nothing half done.</item>
/// </list>
/// Only event ids, types and outcomes are logged: never the payload, a customer or a secret.
/// </summary>
public class StripeWebhookService : IStripeWebhookService
{
    private const long ToleranceSeconds = 300;

    private readonly IStripeWebhookEventRepository _events;
    private readonly IPaymentRepository _payments;
    private readonly IBookingRepository _bookings;
    private readonly IPromoCodeRepository _promoCodes;
    private readonly IBookingConfirmationService _confirmation;
    private readonly StripeSettings _stripe;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IStripeWebhookEventRepository events,
        IPaymentRepository payments,
        IBookingRepository bookings,
        IPromoCodeRepository promoCodes,
        IBookingConfirmationService confirmation,
        IOptions<StripeSettings> stripe,
        TimeProvider timeProvider,
        ILogger<StripeWebhookService> logger)
    {
        _events = events;
        _payments = payments;
        _bookings = bookings;
        _promoCodes = promoCodes;
        _confirmation = confirmation;
        _stripe = stripe.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<StripeWebhookResult> HandleAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!_stripe.IsConfigured)
        {
            // Stripe retries later; by then the secrets may be in place.
            throw new ServiceUnavailableException("Stripe is not configured.");
        }

        Event stripeEvent;
        try
        {
            // The account's API version may differ from the SDK's; only fields that exist in every version are read.
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader ?? string.Empty, _stripe.WebhookSecret, ToleranceSeconds, throwOnApiVersionMismatch: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("A Stripe webhook call was refused: its signature or body is not valid ({ExceptionType}).", exception.GetType().Name);
            return StripeWebhookResult.InvalidSignature;
        }

        var record = await _events.GetByEventIdAsync(stripeEvent.Id, cancellationToken);
        if (record?.ProcessedAt is not null)
        {
            _logger.LogInformation("Stripe event {EventId} ({EventType}) arrived again and was ignored.", stripeEvent.Id, stripeEvent.Type);
            return StripeWebhookResult.Duplicate;
        }

        var session = stripeEvent.Data?.Object as Session;
        if (record is null)
        {
            record = new StripeWebhookEvent { EventId = stripeEvent.Id, Type = Clip(stripeEvent.Type, 100), CheckoutSessionId = session?.Id };
            await _events.AddAsync(record, cancellationToken);
        }

        var outcome = (stripeEvent.Type, session) switch
        {
            (_, null) => "Ignored: not about a Checkout page",
            ("checkout.session.completed", _) when session.PaymentStatus == "paid" => await ConfirmAsync(session, cancellationToken),
            ("checkout.session.completed", _) => "Waiting: the payment is not taken yet",
            ("checkout.session.async_payment_succeeded", _) => await ConfirmAsync(session, cancellationToken),
            ("checkout.session.async_payment_failed", _) => await CloseAsync(session, PaymentStatus.Failed, cancellationToken),
            ("checkout.session.expired", _) => await CloseAsync(session, PaymentStatus.Cancelled, cancellationToken),
            _ => "Ignored: not used"
        };

        record.ProcessedAt = Now();
        record.Outcome = Clip(outcome, 200);

        // One save for the record, the payment and the booking. A clash with another change (the expiry job, say) throws:
        // Stripe gets an error, sends the event again, and it is handled against the new state.
        await _events.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stripe event {EventId} ({EventType}): {Outcome}", stripeEvent.Id, stripeEvent.Type, outcome);
        return StripeWebhookResult.Handled;
    }

    private async Task<string> ConfirmAsync(Session session, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByCheckoutSessionIdAsync(session.Id, cancellationToken);
        if (payment is null)
        {
            return "Ignored: unknown Checkout page";
        }

        if (!ReferencesMatch(session, payment))
        {
            _logger.LogWarning("Stripe event for payment {PaymentId} was rejected: its references do not match.", payment.Id);
            return "Rejected: references do not match";
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return "Already confirmed";
        }

        if (session.AmountTotal != CheckoutService.ToMinorUnits(payment.Amount)
            || !string.Equals(session.Currency, _stripe.Currency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(payment.Currency, AppConstants.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Stripe reported a payment for payment {PaymentId} whose amount or currency does not match; nothing was confirmed. Check it in the Stripe dashboard.", payment.Id);
            return "Rejected: amount or currency does not match";
        }

        // The same tracked booking, with its passengers, tickets and seats.
        var booking = await _bookings.GetByPnrAsync(payment.Booking.Pnr, cancellationToken)
                      ?? throw new InvalidOperationException("The booking of a payment is missing.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = Now();
        payment.StripePaymentIntentId = session.PaymentIntentId;
        payment.ProviderReference = session.PaymentIntentId;
        _payments.Update(payment);

        if (booking.Status == BookingStatus.Pending)
        {
            await CountPromoCodeUseAsync(booking, cancellationToken);
            await _confirmation.ConfirmAsync(booking, payment.Amount, cancellationToken);
            return "Confirmed";
        }

        // The money is taken but the booking cannot be confirmed (it ran out, was cancelled, or was already paid): it must be refunded.
        _logger.LogError("Payment {PaymentId} was taken by Stripe but its booking is {BookingStatus}; it needs a refund.", payment.Id, booking.Status);
        return $"Paid while the booking was {booking.Status}: refund needed";
    }

    private async Task<string> CloseAsync(Session session, PaymentStatus status, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByCheckoutSessionIdAsync(session.Id, cancellationToken);
        if (payment is null)
        {
            return "Ignored: unknown Checkout page";
        }

        if (!ReferencesMatch(session, payment))
        {
            return "Rejected: references do not match";
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return $"Ignored: the payment is already {payment.Status}";
        }

        // The booking keeps its seats until its own time runs out, so another Checkout page can still be opened for it.
        payment.Status = status;
        _payments.Update(payment);
        return status == PaymentStatus.Failed ? "Payment failed" : "Checkout page closed unpaid";
    }

    private async Task CountPromoCodeUseAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.PromoCodeId is not int promoCodeId || await _promoCodes.GetByIdAsync(promoCodeId, cancellationToken) is not { } promoCode)
        {
            return;
        }

        // The limit was checked when the page was opened; the money is taken now, so the use counts even if the limit was reached meanwhile.
        promoCode.UsedCount++;
        _promoCodes.Update(promoCode);
    }

    /// <summary>The page must be the one opened for this very payment and booking.</summary>
    private static bool ReferencesMatch(Session session, Payment payment)
    {
        var paymentId = payment.Id.ToString();
        return session.ClientReferenceId == paymentId
               && session.Metadata is not null
               && session.Metadata.TryGetValue("payment_id", out var metaPayment) && metaPayment == paymentId
               && session.Metadata.TryGetValue("booking_id", out var metaBooking) && metaBooking == payment.BookingId.ToString();
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Clip(string? value, int max) => value is null ? string.Empty : value.Length <= max ? value : value[..max];
}
