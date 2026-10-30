using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>
/// Choosing a seat reserves nothing; only a payment confirmed by Stripe books it, and the first confirmed payment wins the seat.
/// Everything here goes through the real services over in-memory repositories; nothing reaches Stripe.
/// </summary>
public class SeatPaymentRuleTests
{
    private const string Secret = "whsec_unit_tests_only";
    private const int SeatId = 103;
    private readonly BookingTestContext _context = new();

    public SeatPaymentRuleTests()
    {
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = Secret;
    }

    private async Task<(Booking Booking, Payment Payment)> BookAndOpenCheckoutAsync(int userId)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, SeatId)));
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(userId, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(userId, new CreateCheckoutSessionRequestDto { Pnr = pnr });
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        return (booking, booking.Payments.Single());
    }

    private Task<StripeWebhookResult> SendAsync(Payment payment, string eventId, string type = "checkout.session.completed", string paymentStatus = "paid")
    {
        var payload = StripeTestEvents.CheckoutSession(eventId, type, payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId, (long)(payment.Amount * 100), paymentStatus: paymentStatus);
        return _context.CreateWebhookService().HandleAsync(payload, StripeTestEvents.Sign(payload, Secret));
    }

    private SeatStatus Seat => _context.Data.Seat(SeatId).Status;

    [Fact]
    public async Task ChoosingASeatAndOpeningThePayment_LeavesTheSeatFree()
    {
        var (booking, _) = await BookAndOpenCheckoutAsync(1);

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal((SeatStatus.Available, (DateTime?)null), (Seat, _context.Data.Seat(SeatId).HeldUntil));
        Assert.All(booking.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Pending, t.Status));
    }

    [Fact]
    public async Task ASeatOfAnUnpaidBooking_CanBeChosenByAnotherTraveller_AndSeenAsFree()
    {
        await BookAndOpenCheckoutAsync(1);

        var map = await _context.CreateSeatService().GetSeatMapAsync(1);
        Assert.True(map.Seats.Single(s => s.Id == SeatId).IsAvailable);

        var second = await BookAndOpenCheckoutAsync(2);
        Assert.Equal(BookingStatus.Pending, second.Booking.Status);
    }

    [Fact]
    public async Task AFailedPayment_LeavesTheSeatFree_AndConfirmsNothing()
    {
        var (booking, payment) = await BookAndOpenCheckoutAsync(1);

        await SendAsync(payment, "evt_failed", "checkout.session.async_payment_failed", "unpaid");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(SeatStatus.Available, Seat);
        Assert.All(booking.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Pending, t.Status));
    }

    [Fact]
    public async Task ACancelledPaymentPage_LeavesTheSeatFree()
    {
        var (booking, payment) = await BookAndOpenCheckoutAsync(1);

        await SendAsync(payment, "evt_expired", "checkout.session.expired", "unpaid");

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(SeatStatus.Available, Seat);
    }

    [Fact]
    public async Task APaymentConfirmedByStripe_BooksTheSeat_AndIssuesTheTicket()
    {
        var (booking, payment) = await BookAndOpenCheckoutAsync(1);

        await SendAsync(payment, "evt_paid");

        Assert.Equal((BookingStatus.Confirmed, PaymentStatus.Paid, SeatStatus.Booked), (booking.Status, payment.Status, Seat));
        Assert.All(booking.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Issued, t.Status));
        Assert.False(string.IsNullOrWhiteSpace(booking.Pnr));
    }

    [Fact]
    public async Task TwoTravellersChooseTheSameSeat_OnlyTheFirstPaymentGetsIt_AndTheSecondIsRefunded()
    {
        var first = await BookAndOpenCheckoutAsync(1);
        var second = await BookAndOpenCheckoutAsync(2);

        await SendAsync(first.Payment, "evt_first");
        await SendAsync(second.Payment, "evt_second");

        Assert.Equal((BookingStatus.Confirmed, SeatStatus.Booked), (first.Booking.Status, Seat));
        Assert.Equal(TicketStatus.Issued, first.Booking.Passengers.Single().Tickets.Single().Status);

        // The second payment was taken but cannot have the seat: no confirmed booking, the money goes back.
        Assert.Equal(BookingStatus.Cancelled, second.Booking.Status);
        Assert.Equal(TicketStatus.Cancelled, second.Booking.Passengers.Single().Tickets.Single().Status);
        Assert.Equal(PaymentStatus.Refunded, second.Payment.Status);
        var refund = Assert.Single(second.Booking.Cancellation!.Refunds);
        Assert.Equal((second.Payment.Amount, RefundStatus.Completed), (refund.Amount, refund.Status));
        Assert.Equal(refund.Id, Assert.Single(_context.RefundGateway.Requests).RefundId);
        Assert.Equal("Seat taken: booking cancelled, refund requested", _context.WebhookEvents.Events.Single(e => e.EventId == "evt_second").Outcome);
    }

    [Fact]
    public async Task APaymentPageForASeatThatWasSoldMeanwhile_IsNotOpened()
    {
        var first = await BookAndOpenCheckoutAsync(1);
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Nigar", 1992, (1, SeatId)));
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(2, request)).Pnr;

        await SendAsync(first.Payment, "evt_first");

        await Assert.ThrowsAsync<ConflictException>(() =>
            _context.CreateCheckoutService().CreateSessionAsync(2, new CreateCheckoutSessionRequestDto { Pnr = pnr }));
        Assert.Empty((await _context.Bookings.GetByPnrAsync(pnr))!.Payments);
    }

    [Fact]
    public async Task ASeatThatIsAlreadyBooked_CannotBeChosenForANewBooking()
    {
        var first = await BookAndOpenCheckoutAsync(1);
        await SendAsync(first.Payment, "evt_first");

        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Nigar", 1992, (1, SeatId)));
        await Assert.ThrowsAsync<ConflictException>(() => _context.CreateBookingService().CreateBookingAsync(2, request));
    }

    [Fact]
    public async Task ThePaymentCallbackArrivingAgain_CreatesNothingTwice()
    {
        var (booking, payment) = await BookAndOpenCheckoutAsync(1);

        await SendAsync(payment, "evt_paid");
        var again = await SendAsync(payment, "evt_paid");
        var anotherEventForTheSamePage = await SendAsync(payment, "evt_paid_async", "checkout.session.async_payment_succeeded");

        Assert.Equal(StripeWebhookResult.Duplicate, again);
        Assert.Equal(StripeWebhookResult.Handled, anotherEventForTheSamePage);
        Assert.Single(await _context.Bookings.GetAllAsync());
        Assert.Equal((BookingStatus.Confirmed, SeatStatus.Booked), (booking.Status, Seat));
        Assert.Empty(_context.RefundGateway.Requests);
    }

    [Fact]
    public async Task AnUnpaidBookingRunningOut_NeverFreesASeatSomebodyElsePaidFor()
    {
        var unpaid = await BookAndOpenCheckoutAsync(2);
        var paid = await BookAndOpenCheckoutAsync(1);
        await SendAsync(paid.Payment, "evt_paid");

        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(40));
        await _context.CreateExpiryService().ReleaseExpiredAsync();

        Assert.Equal(BookingStatus.Expired, unpaid.Booking.Status);
        Assert.Equal((BookingStatus.Confirmed, SeatStatus.Booked), (paid.Booking.Status, Seat));
    }

    [Fact]
    public async Task CancellingAnUnpaidBooking_NeverFreesASeatSomebodyElsePaidFor()
    {
        var unpaid = await BookAndOpenCheckoutAsync(2);
        var paid = await BookAndOpenCheckoutAsync(1);
        await SendAsync(paid.Payment, "evt_paid");

        await _context.CreateCancellationService().CancelOwnBookingAsync(unpaid.Booking.Pnr, 2, "Changed my mind", null);

        Assert.Equal(BookingStatus.Cancelled, unpaid.Booking.Status);
        Assert.Equal(SeatStatus.Booked, Seat);
    }
}
