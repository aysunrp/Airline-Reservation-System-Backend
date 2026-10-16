using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;

namespace Service.Tests;

/// <summary>Bookings depart on 10 Nov 2026; "now" is 1 Nov, i.e. 9 days before departure.</summary>
public class CancellationServiceTests
{
    private readonly BookingTestContext _context = new();

    private async Task<BookingDto> CreateBookingAsync(CabinClass cabin, int seatId, bool pay)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, seatId)));
        request.Legs[0].CabinClass = cabin;
        var booking = await _context.CreateBookingService().CreateBookingAsync(1, request);

        return pay ? await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr)) : booking;
    }

    private static CancelBookingRequestDto Cancel(string pnr, string lastName = "Panahli") => new()
    {
        Pnr = pnr,
        LastName = lastName,
        Reason = "Change of plans"
    };

    [Fact]
    public async Task PaidEconomy_InsideNoRefundWindow_CancelsWithoutRefund()
    {
        var booking = await CreateBookingAsync(CabinClass.Economy, 103, pay: true);
        var service = _context.CreateCancellationService();

        var quote = await service.GetQuoteAsync(booking.Pnr, "panahli");
        Assert.Equal(9, quote.DaysBeforeDeparture);
        Assert.False(quote.IsRefundable);
        Assert.Equal(0m, quote.RefundAmount);

        var result = await service.CancelAsync(Cancel(booking.Pnr));

        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal(booking.Price.Total, result.Cancellation!.AmountPaid);
        Assert.Equal(0m, result.Cancellation.RefundAmount);
        Assert.Equal(RefundStatus.NotEligible, result.Cancellation.RefundStatus);
        Assert.All(result.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Cancelled, t.Status));
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Contains(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.BookingCancelled);
    }

    [Fact]
    public async Task PaidBusiness_MoreThanSevenDaysAhead_RequestsFullRefund()
    {
        var booking = await CreateBookingAsync(CabinClass.Business, 101, pay: true);

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr));

        Assert.Equal(0m, result.Cancellation!.FeeAmount);
        Assert.Equal(booking.Price.Total, result.Cancellation.RefundAmount);
        Assert.Equal(RefundStatus.Pending, result.Cancellation.RefundStatus);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(101).Status);

        // My Trips shows the refund state of the cancelled booking.
        var trip = Assert.Single(await _context.CreateBookingService().GetMyBookingsAsync(1));
        Assert.Equal(RefundStatus.Pending, trip.RefundStatus);
    }

    [Fact]
    public async Task UnpaidBooking_IsCancelledWithoutRefundRequest()
    {
        var booking = await CreateBookingAsync(CabinClass.Economy, 103, pay: false);

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr));

        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal(0m, result.Cancellation!.AmountPaid);
        Assert.Null(result.Cancellation.RefundStatus);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Null(_context.Data.Seat(103).HeldUntil);
    }

    [Fact]
    public async Task CancellingTwice_ThrowsConflict()
    {
        var booking = await CreateBookingAsync(CabinClass.Economy, 103, pay: true);
        var service = _context.CreateCancellationService();
        await service.CancelAsync(Cancel(booking.Pnr));

        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(Cancel(booking.Pnr)));
    }

    [Fact]
    public async Task WrongLastName_IsNotFound()
    {
        var booking = await CreateBookingAsync(CabinClass.Economy, 103, pay: true);

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr, "Aliyev")));
        Assert.Equal(BookingStatus.Confirmed, (await _context.Bookings.GetByPnrAsync(booking.Pnr))!.Status);
    }

    [Fact]
    public async Task DepartedTrip_CannotBeCancelled()
    {
        var booking = await CreateBookingAsync(CabinClass.Economy, 103, pay: true);
        _context.Clock = new FixedTimeProvider(TestData.OutboundDeparture.AddHours(1));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr)));
    }
}
