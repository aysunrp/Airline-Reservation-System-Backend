using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>Signed events as Stripe would send them, against bookings opened through Checkout. Nothing reaches Stripe.</summary>
public class StripeWebhookServiceTests
{
    private const int Owner = 1;
    private const string Secret = "whsec_unit_tests_only";
    private readonly BookingTestContext _context = new();

    public StripeWebhookServiceTests()
    {
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = Secret;
    }

    /// <summary>A pending booking (with the AEROVA10 promo code) and its open Stripe page.</summary>
    private async Task<(Booking Booking, Payment Payment)> OpenCheckoutAsync()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        request.PromoCode = "AEROVA10";
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(Owner, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(Owner, new CreateCheckoutSessionRequestDto { Pnr = pnr });
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        return (booking, booking.Payments.Single());
    }

    private static long Cents(Payment payment) => (long)(payment.Amount * 100);

    private string Completed(Payment payment, string eventId = "evt_test_paid", string type = "checkout.session.completed",
        long? amount = null, string currency = "usd", string paymentStatus = "paid", int? metadataPaymentId = null)
        => StripeTestEvents.CheckoutSession(eventId, type, payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId,
            amount ?? Cents(payment), currency, paymentStatus, metadataPaymentId: metadataPaymentId);

    private Task<StripeWebhookResult> SendAsync(string payload, string? signature = null)
        => _context.CreateWebhookService().HandleAsync(payload, signature ?? StripeTestEvents.Sign(payload, Secret));

    [Fact]
    public async Task APaidPage_ConfirmsTheBooking_IssuesTickets_BooksTheSeat_AndCountsThePromoCode()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        var result = await SendAsync(Completed(payment));

        Assert.Equal(StripeWebhookResult.Handled, result);
        Assert.Equal((BookingStatus.Confirmed, (DateTime?)null), (booking.Status, booking.ExpiresAt));
        Assert.Equal((PaymentStatus.Paid, "pi_test_0001", "pi_test_0001"), (payment.Status, payment.StripePaymentIntentId, payment.ProviderReference));
        Assert.NotNull(payment.PaidAt);
        Assert.All(booking.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Issued, t.Status));
        Assert.Equal(SeatStatus.Booked, _context.Data.Seat(103).Status);
        Assert.Equal(4, (await _context.PromoCodes.GetByIdAsync(5))!.UsedCount);
        Assert.Equal(new[] { NotificationType.BookingConfirmed, NotificationType.PaymentSuccessful }, (await _context.Notifications.GetAllAsync()).Select(n => n.Type));

        var record = Assert.Single(_context.WebhookEvents.Events);
        Assert.Equal(("evt_test_paid", "checkout.session.completed", "Confirmed"), (record.EventId, record.Type, record.Outcome));
        Assert.NotNull(record.ProcessedAt);
    }

    private Stripe.Checkout.Session StripeAnswer(Payment payment, string paymentStatus, string status, long? amount = null)
        => new()
        {
            Id = payment.StripeCheckoutSessionId!,
            PaymentStatus = paymentStatus,
            Status = status,
            AmountTotal = amount ?? Cents(payment),
            Currency = "usd",
            PaymentIntentId = "pi_test_0001",
            ClientReferenceId = payment.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["payment_id"] = payment.Id.ToString(), ["booking_id"] = payment.BookingId.ToString() }
        };

    [Fact]
    public async Task ReconcilingAPaidPage_ConfirmsTheBookingJustLikeTheWebhook()
    {
        var (booking, payment) = await OpenCheckoutAsync();
        _context.Gateway.Known[payment.StripeCheckoutSessionId!] = StripeAnswer(payment, "paid", "complete");

        var changed = await _context.CreateWebhookService().ReconcileAsync(payment.StripeCheckoutSessionId!);

        Assert.True(changed);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(SeatStatus.Booked, _context.Data.Seat(103).Status);
    }

    [Fact]
    public async Task ReconcilingAnUnpaidOrUnknownPage_ChangesNothing()
    {
        var (booking, payment) = await OpenCheckoutAsync();
        var service = _context.CreateWebhookService();

        Assert.False(await service.ReconcileAsync(payment.StripeCheckoutSessionId!));

        _context.Gateway.Known[payment.StripeCheckoutSessionId!] = StripeAnswer(payment, "unpaid", "open");
        Assert.False(await service.ReconcileAsync(payment.StripeCheckoutSessionId!));

        Assert.Equal((BookingStatus.Pending, PaymentStatus.Pending), (booking.Status, payment.Status));
    }

    [Fact]
    public async Task ReconcilingAPageWithAnotherAmount_ConfirmsNothing()
    {
        var (booking, payment) = await OpenCheckoutAsync();
        _context.Gateway.Known[payment.StripeCheckoutSessionId!] = StripeAnswer(payment, "paid", "complete", amount: 100);

        var changed = await _context.CreateWebhookService().ReconcileAsync(payment.StripeCheckoutSessionId!);

        Assert.False(changed);
        Assert.Equal((BookingStatus.Pending, PaymentStatus.Pending), (booking.Status, payment.Status));
    }

    [Fact]
    public async Task TheSameEventAgain_ChangesNothing()
    {
        var (_, payment) = await OpenCheckoutAsync();
        var payload = Completed(payment);
        await SendAsync(payload);

        var again = await SendAsync(payload);

        Assert.Equal(StripeWebhookResult.Duplicate, again);
        Assert.Equal(2, (await _context.Notifications.GetAllAsync()).Count);
        Assert.Equal(4, (await _context.PromoCodes.GetByIdAsync(5))!.UsedCount);
        Assert.Single(_context.WebhookEvents.Events);
    }

    [Fact]
    public async Task ASecondEventForAPaidPage_ConfirmsNothingTwice()
    {
        var (booking, payment) = await OpenCheckoutAsync();
        await SendAsync(Completed(payment));

        var result = await SendAsync(Completed(payment, eventId: "evt_test_async", type: "checkout.session.async_payment_succeeded"));

        Assert.Equal(StripeWebhookResult.Handled, result);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(2, (await _context.Notifications.GetAllAsync()).Count);
        Assert.Equal(4, (await _context.PromoCodes.GetByIdAsync(5))!.UsedCount);
        Assert.Equal("Already confirmed", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_test_async").Outcome);
    }

    [Theory]
    [InlineData("wrong-secret", false, 0)]
    [InlineData(Secret, true, 0)]
    [InlineData(Secret, false, -600)]
    public async Task AnUnprovenCall_IsRefused_AndChangesNothing(string secret, bool tamper, int secondsAgo)
    {
        var (booking, payment) = await OpenCheckoutAsync();
        var payload = Completed(payment);
        var signature = StripeTestEvents.Sign(payload, secret, DateTimeOffset.UtcNow.AddSeconds(secondsAgo));
        if (tamper)
        {
            payload = payload.Replace(Cents(payment).ToString(), "1");
        }

        var result = await SendAsync(payload, signature);

        Assert.Equal(StripeWebhookResult.InvalidSignature, result);
        Assert.Equal((BookingStatus.Pending, PaymentStatus.Pending), (booking.Status, payment.Status));
        Assert.Empty(_context.WebhookEvents.Events);
    }

    [Fact]
    public async Task ACallWithoutASignature_IsRefused()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        Assert.Equal(StripeWebhookResult.InvalidSignature, await SendAsync(Completed(payment), signature: string.Empty));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Theory]
    [InlineData(1, "usd")]
    [InlineData(0, "eur")]
    public async Task AWrongAmountOrCurrency_ConfirmsNothing(long centsOff, string currency)
    {
        var (booking, payment) = await OpenCheckoutAsync();

        var result = await SendAsync(Completed(payment, amount: Cents(payment) - centsOff, currency: currency));

        Assert.Equal(StripeWebhookResult.Handled, result);
        Assert.Equal((BookingStatus.Pending, PaymentStatus.Pending), (booking.Status, payment.Status));
        Assert.StartsWith("Rejected", _context.WebhookEvents.Events.Single().Outcome);
        Assert.Empty(await _context.Notifications.GetAllAsync());
    }

    [Fact]
    public async Task APageWhoseReferencesPointElsewhere_ConfirmsNothing()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        await SendAsync(Completed(payment, metadataPaymentId: payment.Id + 1));

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal("Rejected: references do not match", _context.WebhookEvents.Events.Single().Outcome);
    }

    [Fact]
    public async Task ACompletedPageWithoutTheMoneyYet_Waits()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        await SendAsync(Completed(payment, paymentStatus: "unpaid"));

        Assert.Equal((BookingStatus.Pending, PaymentStatus.Pending), (booking.Status, payment.Status));
    }

    [Fact]
    public async Task AFailedPayment_IsRecorded_AndTheBookingCanStillBePaid()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        await SendAsync(Completed(payment, eventId: "evt_test_failed", type: "checkout.session.async_payment_failed", paymentStatus: "unpaid"));

        Assert.Equal((BookingStatus.Pending, PaymentStatus.Failed), (booking.Status, payment.Status));
        var next = await _context.CreateCheckoutService().CreateSessionAsync(Owner, new CreateCheckoutSessionRequestDto { Pnr = booking.Pnr });
        Assert.NotEqual(payment.Id, next.PaymentId);
    }

    [Fact]
    public async Task AnExpiredPage_ClosesItsPayment()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        await SendAsync(Completed(payment, eventId: "evt_test_expired", type: "checkout.session.expired", paymentStatus: "unpaid"));

        Assert.Equal((BookingStatus.Pending, PaymentStatus.Cancelled), (booking.Status, payment.Status));
    }

    [Fact]
    public async Task APaymentAfterTheBookingRanOut_IsRecordedButConfirmsNothing()
    {
        var (booking, payment) = await OpenCheckoutAsync();
        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(34));
        await _context.CreateExpiryService().ReleaseExpiredAsync();

        await SendAsync(Completed(payment));

        Assert.Equal((BookingStatus.Expired, PaymentStatus.Paid), (booking.Status, payment.Status));
        Assert.Equal("Paid while the booking was Expired: refund needed", _context.WebhookEvents.Events.Single().Outcome);
        Assert.Empty(await _context.Notifications.GetAllAsync());
    }

    [Fact]
    public async Task EventsAboutOtherThings_AreAcknowledged_AndIgnored()
    {
        var (booking, payment) = await OpenCheckoutAsync();

        Assert.Equal(StripeWebhookResult.Handled, await SendAsync(StripeTestEvents.Other("evt_test_customer", "customer.created")));
        Assert.Equal(StripeWebhookResult.Handled, await SendAsync(StripeTestEvents.CheckoutSession("evt_test_unknown", "checkout.session.completed",
            "cs_test_not_ours", payment.Id, payment.BookingId, Cents(payment))));

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(new[] { "Ignored: not about a Checkout page", "Ignored: unknown Checkout page" }, _context.WebhookEvents.Events.Select(e => e.Outcome));
    }

    [Fact]
    public async Task WithoutStripeSecrets_TheCallFails_SoStripeTriesAgainLater()
    {
        _context.Stripe.WebhookSecret = null;
        var payload = StripeTestEvents.Other("evt_test_x", "customer.created");

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => SendAsync(payload, StripeTestEvents.Sign(payload, Secret)));
    }
}
