using Domain.Enums;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

public class CheckoutServiceTests
{
    private const int Owner = 1;
    private readonly BookingTestContext _context = new();

    public CheckoutServiceTests()
    {
        // Test values only; no request ever leaves the process (the gateway is a fake).
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = "whsec_unit";
    }

    private async Task<string> CreateBookingAsync(int? userId = Owner, string? promoCode = null)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        request.PromoCode = promoCode;
        return (await _context.CreateBookingService().CreateBookingAsync(userId, request)).Pnr;
    }

    private static CreateCheckoutSessionRequestDto For(string pnr, string? lastName = null) => new() { Pnr = pnr, LastName = lastName };

    [Fact]
    public async Task WithoutStripeSecrets_CheckoutIsUnavailable()
    {
        var pnr = await CreateBookingAsync();
        _context.Stripe.WebhookSecret = null;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr)));
        Assert.Empty(_context.Gateway.Created);
    }

    [Fact]
    public async Task Owner_GetsAStripePage_ForTheBookingsOwnTotal_InCents()
    {
        var pnr = await CreateBookingAsync(promoCode: "AEROVA10");
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;

        var session = await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr.ToLowerInvariant()));

        var sent = Assert.Single(_context.Gateway.Created);
        Assert.Equal((long)(booking.TotalAmount * 100), sent.AmountMinor);
        Assert.Equal("usd", sent.Currency);
        Assert.Equal(pnr, sent.Pnr);
        Assert.Equal($"https://aerova.test/payment-success.html?payment={session.PaymentId}&session={{CHECKOUT_SESSION_ID}}", sent.SuccessUrl);
        Assert.Equal($"https://aerova.test/payment-cancel.html?payment={session.PaymentId}", sent.CancelUrl);
        Assert.Equal(TestData.Now.AddMinutes(31), sent.ExpiresAtUtc);

        var payment = Assert.Single(booking.Payments);
        Assert.Equal((PaymentStatus.Pending, PaymentMethod.StripeCheckout, booking.TotalAmount, "USD"), (payment.Status, payment.Method, payment.Amount, payment.Currency));
        Assert.Equal(session.CheckoutUrl, payment.CheckoutUrl);
        Assert.StartsWith("cs_test_", payment.StripeCheckoutSessionId);
        Assert.Null(payment.PaidAt);

        // Opening the page confirms nothing.
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(SeatStatus.Held, _context.Data.Seat(103).Status);
    }

    [Fact]
    public async Task OpeningThePage_KeepsTheSeatsHeldUntilItCloses()
    {
        var pnr = await CreateBookingAsync();

        await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));

        var holdUntil = TestData.Now.AddMinutes(31 + 2);
        Assert.Equal(holdUntil, (await _context.Bookings.GetByPnrAsync(pnr))!.ExpiresAt);
        Assert.Equal(holdUntil, _context.Data.Seat(103).HeldUntil);
    }

    [Fact]
    public async Task SomebodyElse_OrNobody_CannotPayAnAccountsBooking()
    {
        var pnr = await CreateBookingAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCheckoutService().CreateSessionAsync(2, For(pnr)));
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCheckoutService().CreateSessionAsync(null, For(pnr, "Panahli")));
        Assert.Empty(_context.Gateway.Created);
    }

    [Fact]
    public async Task AGuestBooking_NeedsAPassengersLastName()
    {
        var pnr = await CreateBookingAsync(userId: null);

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCheckoutService().CreateSessionAsync(null, For(pnr, "Someone")));
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCheckoutService().CreateSessionAsync(null, For(pnr)));

        var session = await _context.CreateCheckoutService().CreateSessionAsync(null, For(pnr, " panahli "));
        Assert.StartsWith("https://checkout.stripe.test/", session.CheckoutUrl);
    }

    [Fact]
    public async Task AskingAgain_WhileThePageIsOpen_OffersTheSamePage()
    {
        var pnr = await CreateBookingAsync();
        var service = _context.CreateCheckoutService();

        var first = await service.CreateSessionAsync(Owner, For(pnr));
        var second = await service.CreateSessionAsync(Owner, For(pnr));

        Assert.Equal((first.PaymentId, first.CheckoutUrl), (second.PaymentId, second.CheckoutUrl));
        Assert.Single(_context.Gateway.Created);
        Assert.Single((await _context.Bookings.GetByPnrAsync(pnr))!.Payments);
    }

    [Fact]
    public async Task APaidBooking_GetsNoSecondPage()
    {
        var pnr = await CreateBookingAsync();
        // Paid the pretend way, which only works while Stripe is not configured.
        _context.Stripe.SecretKey = null;
        await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(pnr));
        _context.Stripe.SecretKey = "sk_test_unit";

        await Assert.ThrowsAsync<ConflictException>(() => _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr)));
        Assert.Empty(_context.Gateway.Created);
    }

    [Fact]
    public async Task ARunOutHold_IsReleased_AndNotSentToStripe()
    {
        var pnr = await CreateBookingAsync();
        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(16));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr)));

        Assert.Equal(BookingStatus.Expired, (await _context.Bookings.GetByPnrAsync(pnr))!.Status);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Empty(_context.Gateway.Created);
    }

    [Fact]
    public async Task WhenStripeFails_ThePaymentIsClosed_AndATryLaterOpensANewOne()
    {
        var pnr = await CreateBookingAsync();
        _context.Gateway.Fail = true;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr)));
        var failed = Assert.Single((await _context.Bookings.GetByPnrAsync(pnr))!.Payments);
        Assert.Equal(PaymentStatus.Cancelled, failed.Status);

        _context.Gateway.Fail = false;
        var session = await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));

        Assert.NotEqual(failed.Id, session.PaymentId);
    }

    [Fact]
    public async Task AClosedPage_IsReplacedByANewOne()
    {
        var pnr = await CreateBookingAsync();
        var first = await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));

        // The page closed (31 minutes); the hold is 2 minutes longer, so the booking can still be paid.
        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(32));
        var second = await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));

        Assert.NotEqual(first.PaymentId, second.PaymentId);
        var payments = (await _context.Bookings.GetByPnrAsync(pnr))!.Payments.OrderBy(p => p.Id).ToList();
        Assert.Equal(new[] { PaymentStatus.Cancelled, PaymentStatus.Pending }, payments.Select(p => p.Status));
    }

    [Fact]
    public async Task Status_IsShownToTheOwner_AnAdmin_OrWithTheSessionId_AndHiddenFromOthers()
    {
        var pnr = await CreateBookingAsync();
        var session = await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));
        var sessionId = (await _context.Bookings.GetByPnrAsync(pnr))!.Payments.Single().StripeCheckoutSessionId;
        var service = _context.CreateCheckoutService();

        var mine = await service.GetStatusAsync(session.PaymentId, Owner, false, null);
        Assert.Equal((PaymentStatus.Pending, BookingStatus.Pending, pnr, "USD"), (mine.Status, mine.BookingStatus, mine.Pnr, mine.Currency));
        Assert.Equal(PaymentStatus.Pending, (await service.GetStatusAsync(session.PaymentId, null, true, null)).Status);
        Assert.Equal(PaymentStatus.Pending, (await service.GetStatusAsync(session.PaymentId, null, false, sessionId)).Status);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStatusAsync(session.PaymentId, 2, false, null));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStatusAsync(session.PaymentId, null, false, "cs_test_wrong"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStatusAsync(999_999, Owner, false, null));
    }

    [Fact]
    public async Task TheBookingRunningOut_ClosesItsUnpaidPayment()
    {
        var pnr = await CreateBookingAsync();
        await _context.CreateCheckoutService().CreateSessionAsync(Owner, For(pnr));

        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(34));
        await _context.CreateExpiryService().ReleaseExpiredAsync();

        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.Equal(PaymentStatus.Cancelled, booking.Payments.Single().Status);
    }

    [Theory]
    [InlineData(462.00, 46200)]
    [InlineData(123.45, 12345)]
    [InlineData(0.01, 1)]
    public void Amounts_BecomeWholeCents(decimal amount, long cents)
        => Assert.Equal(cents, CheckoutService.ToMinorUnits(amount));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public void AmountsThatCannotBeCharged_AreRefused(decimal amount)
        => Assert.Throws<BusinessRuleException>(() => CheckoutService.ToMinorUnits(amount));

    [Fact]
    public async Task ThePretendPayment_IsRefusedOnceStripeIsConfigured()
    {
        var pnr = await CreateBookingAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(pnr)));
        Assert.Empty((await _context.Bookings.GetByPnrAsync(pnr))!.Payments);
    }

    [Fact]
    public async Task ThePretendPayment_IsRefusedWhereItIsNotAllowed()
    {
        var pnr = await CreateBookingAsync();
        _context.Stripe.SecretKey = null;
        _context.PaymentSettings.AllowMockPayments = false;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(pnr)));
    }
}
