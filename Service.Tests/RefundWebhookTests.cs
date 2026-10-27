using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>Signed refund events as Stripe would send them, against refunds AEROVA asked for. Nothing reaches Stripe.</summary>
public class RefundWebhookTests
{
    private const int Owner = 1;
    private const string Secret = "whsec_unit_tests_only";
    private readonly BookingTestContext _context = new();

    public RefundWebhookTests()
    {
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = Secret;

        // Stripe takes the request but the refund is still on its way.
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
    }

    private async Task<(Booking Booking, Payment Payment, Refund Refund)> CancelledStripeBookingAsync(int seatId = 101, CabinClass cabin = CabinClass.Business)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, seatId)));
        request.Legs[0].CabinClass = cabin;
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(Owner, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(Owner, new CreateCheckoutSessionRequestDto { Pnr = pnr, LastName = "Panahli" });
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        var payment = booking.Payments.Single();
        var paid = StripeTestEvents.CheckoutSession($"evt_pay_{pnr}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, booking.Id, (long)(payment.Amount * 100));
        await SendAsync(paid);

        await _context.CreateCancellationService().CancelAsync(new CancelBookingRequestDto { Pnr = pnr, LastName = "Panahli" }, Owner);
        return (booking, payment, Assert.Single(_context.Refunds.All));
    }

    private Task<StripeWebhookResult> SendAsync(string payload, string? signature = null)
        => _context.CreateWebhookService().HandleAsync(payload, signature ?? StripeTestEvents.Sign(payload, Secret));

    private static string Event(Refund refund, Payment payment, string eventId, string type, string status, long? amount = null, string currency = "usd",
        string? paymentIntent = null, string? failureReason = null, string? refundId = null, bool withMetadata = true)
        => StripeTestEvents.Refund(eventId, type, refundId ?? refund.ProviderReference ?? $"re_test_{refund.Id}", status, amount ?? (long)(refund.Amount * 100),
            paymentIntent ?? payment.StripePaymentIntentId!, withMetadata ? refund.Id : null, currency, failureReason);

    [Fact]
    public async Task ASucceededRefund_CompletesIt_RefundsThePayment_AndTellsTheCustomer()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();
        Assert.Equal((RefundStatus.Processing, PaymentStatus.Paid), (refund.Status, payment.Status));

        var result = await SendAsync(Event(refund, payment, "evt_ok", "refund.updated", "succeeded"));

        Assert.Equal(StripeWebhookResult.Handled, result);
        Assert.Equal((RefundStatus.Completed, PaymentStatus.Refunded, "succeeded"), (refund.Status, payment.Status, refund.ProviderStatus));
        Assert.NotNull(refund.ProcessedAt);
        Assert.Single(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
        Assert.Equal("Completed", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_ok").Outcome);
    }

    [Fact]
    public async Task TheSameEventTwice_IsHandledOnce_AndALaterPendingNeverUndoesTheResult()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();
        var payload = Event(refund, payment, "evt_dup", "refund.updated", "succeeded");
        await SendAsync(payload);

        Assert.Equal(StripeWebhookResult.Duplicate, await SendAsync(payload));
        Assert.Single(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);

        // Events can arrive out of order: "pending" after "succeeded" changes nothing.
        await SendAsync(Event(refund, payment, "evt_late", "refund.updated", "pending"));
        Assert.Equal((RefundStatus.Completed, PaymentStatus.Refunded), (refund.Status, payment.Status));
        Assert.StartsWith("Ignored", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_late").Outcome);
    }

    [Fact]
    public async Task AFailedRefund_IsRecordedWithItsReason_AndThePaymentStaysPaid()
    {
        var (booking, payment, refund) = await CancelledStripeBookingAsync();

        await SendAsync(Event(refund, payment, "evt_failed", "refund.failed", "failed", failureReason: "expired_or_canceled_card"));

        Assert.Equal((RefundStatus.Failed, "expired_or_canceled_card"), (refund.Status, refund.FailureReason));
        Assert.Equal((PaymentStatus.Paid, BookingStatus.Cancelled), (payment.Status, booking.Status));
        Assert.DoesNotContain(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task ARefundThatFailsAfterItSucceeded_IsReopened_BecauseTheMoneyCameBack()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();
        await SendAsync(Event(refund, payment, "evt_ok", "refund.updated", "succeeded"));

        await SendAsync(Event(refund, payment, "evt_back", "refund.failed", "failed", failureReason: "lost_or_stolen_card"));

        Assert.Equal((RefundStatus.Failed, PaymentStatus.Paid), (refund.Status, payment.Status));
        Assert.Null(refund.ProcessedAt);
        Assert.StartsWith("Failed after it had completed", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_back").Outcome);
    }

    [Fact]
    public async Task ACancelledRefund_IsMarkedCancelled_AndCanBeSentAgain()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();

        await SendAsync(Event(refund, payment, "evt_cancel", "refund.updated", "canceled"));

        Assert.Equal(RefundStatus.Cancelled, refund.Status);
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}_{r.Attempt}", "succeeded", null);
        var retried = await _context.CreateRefundService().RetryAsync(refund.Id, new AuditActor(9, "SuperAdmin"));
        Assert.Equal(RefundStatus.Completed, retried.Status);
    }

    [Fact]
    public async Task TheEventCanArriveBeforeTheAnswerToTheRequest_AndIsMatchedByAerovasOwnReference()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();

        // The answer to the request was lost: AEROVA has no Stripe refund id yet.
        refund.ProviderReference = null;
        refund.ProviderStatus = null;
        refund.Status = RefundStatus.Pending;

        await SendAsync(Event(refund, payment, "evt_early", "refund.created", "succeeded", refundId: "re_early_0001"));

        Assert.Equal((RefundStatus.Completed, "re_early_0001"), (refund.Status, refund.ProviderReference));
    }

    [Fact]
    public async Task ARefundOfAnotherPaymentOrAmountOrCurrency_IsRejectedAndChangesNothing()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();

        await SendAsync(Event(refund, payment, "evt_pi", "refund.updated", "succeeded", paymentIntent: "pi_someone_else"));
        await SendAsync(Event(refund, payment, "evt_amount", "refund.updated", "succeeded", amount: (long)(refund.Amount * 100) - 1));
        await SendAsync(Event(refund, payment, "evt_currency", "refund.updated", "succeeded", currency: "eur"));
        await SendAsync(Event(refund, payment, "evt_other_id", "refund.updated", "succeeded", refundId: "re_not_ours"));

        Assert.All(new[] { "evt_pi", "evt_amount", "evt_currency", "evt_other_id" }, id => Assert.StartsWith("Rejected", _context.WebhookEvents.Events.Single(e => e.EventId == id).Outcome));
        Assert.Equal((RefundStatus.Processing, PaymentStatus.Paid), (refund.Status, payment.Status));
        Assert.DoesNotContain(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task ARefundAerovaDidNotAskFor_IsAcknowledgedAndIgnored()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();

        // Made by hand in the Stripe dashboard: no metadata, an unknown refund id.
        var payload = Event(refund, payment, "evt_manual", "refund.created", "succeeded", refundId: "re_by_hand", withMetadata: false);
        Assert.Equal(StripeWebhookResult.Handled, await SendAsync(payload));

        Assert.Equal("Ignored: not a refund AEROVA asked for", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_manual").Outcome);
        Assert.Equal(RefundStatus.Processing, refund.Status);
    }

    [Fact]
    public async Task ARefundEventWithABadSignature_IsRefusedAndChangesNothing()
    {
        var (_, payment, refund) = await CancelledStripeBookingAsync();
        var payload = Event(refund, payment, "evt_bad", "refund.updated", "succeeded");

        Assert.Equal(StripeWebhookResult.InvalidSignature, await SendAsync(payload, StripeTestEvents.Sign(payload, "whsec_someone_else")));
        Assert.Equal(StripeWebhookResult.InvalidSignature, await SendAsync(payload, "t=1,v1=00"));

        Assert.Equal(RefundStatus.Processing, refund.Status);
        Assert.DoesNotContain(_context.WebhookEvents.Events, e => e.EventId == "evt_bad");
    }

    [Fact]
    public async Task APartialRefund_LeavesThePaymentPaid()
    {
        // Economy 40 days out would pay back part of the fare; the test rules give Business a part after day 1, so move the clock.
        var (_, payment, refund) = await CancelledStripeBookingAsync();
        refund.Amount = payment.Amount - 50m;
        refund.Cancellation.RefundAmount = refund.Amount;

        await SendAsync(Event(refund, payment, "evt_part", "refund.updated", "succeeded", amount: (long)(refund.Amount * 100)));

        Assert.Equal((RefundStatus.Completed, PaymentStatus.Paid), (refund.Status, payment.Status));
    }
}
