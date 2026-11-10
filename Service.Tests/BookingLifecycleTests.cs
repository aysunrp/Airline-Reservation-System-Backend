using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>
/// Mock refunds and trip completion. Bookings are made on 1 Nov 2026 for the 10 Nov outbound flight
/// (lands 17:55 UTC) and, for round trips, the 17 Nov return.
/// </summary>
public class BookingLifecycleTests
{
    private readonly BookingTestContext _context = new();

    private RefundProcessingService CreateRefunds(DateTime now) => new(
        _context.Bookings,
        new NotificationService(_context.Notifications),
        Options.Create(new BookingLifecycleSettings()),
        new FixedTimeProvider(now));

    private TripCompletionService CreateCompletion(DateTime now) => new(_context.Data.Flights(), _context.Bookings, new FixedTimeProvider(now));

    private async Task<BookingDto> BookAndPayAsync(CreateBookingRequestDto request)
    {
        var booking = await _context.CreateBookingService().CreateBookingAsync(1, request);
        return await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr));
    }

    private Task<BookingDto> BookAndPayBusinessAsync()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 101)));
        request.Legs[0].CabinClass = CabinClass.Business;
        return BookAndPayAsync(request);
    }

    private async Task<DateTime> CancelAsync(string pnr, DateTime when)
    {
        _context.Clock = new FixedTimeProvider(when);
        await _context.CreateCancellationService().CancelAsync(new CancelBookingRequestDto { Pnr = pnr, LastName = "Panahli" }, 1);
        return when;
    }

    [Fact]
    public async Task FullRefund_GoesPendingProcessingCompleted_AndThePaymentIsRefunded()
    {
        var booking = await BookAndPayBusinessAsync();
        var cancelledAt = await CancelAsync(booking.Pnr, TestData.Now);
        var stored = (await _context.Bookings.GetByPnrAsync(booking.Pnr))!;
        var refund = stored.Cancellation!.Refunds.Single();

        Assert.Equal(new RefundProcessingResult(0, 0), await CreateRefunds(cancelledAt.AddMinutes(4)).ProcessDueAsync());
        Assert.Equal(RefundStatus.Pending, refund.Status);

        Assert.Equal(new RefundProcessingResult(1, 0), await CreateRefunds(cancelledAt.AddMinutes(5)).ProcessDueAsync());
        Assert.Equal(RefundStatus.Processing, refund.Status);

        Assert.Equal(new RefundProcessingResult(0, 1), await CreateRefunds(cancelledAt.AddMinutes(30)).ProcessDueAsync());
        Assert.Equal(RefundStatus.Completed, refund.Status);
        Assert.Equal(cancelledAt.AddMinutes(30), refund.ProcessedAt);
        Assert.StartsWith("MOCK-REFUND-", refund.ProviderReference);
        Assert.Equal(PaymentStatus.Refunded, stored.Payments.Single().Status);
        Assert.Contains(await _context.Notifications.GetByUserIdAsync(1), n => n.Type == NotificationType.RefundCompleted && n.Message.Contains(booking.Pnr));

        // Nothing is left to do afterwards, and My Trips shows the finished refund.
        Assert.Equal(new RefundProcessingResult(0, 0), await CreateRefunds(cancelledAt.AddHours(2)).ProcessDueAsync());
        Assert.Equal(RefundStatus.Completed, (await _context.CreateBookingService().GetMyBookingsAsync(1)).Single().RefundStatus);
    }

    [Fact]
    public async Task PartialRefund_CompletesButLeavesThePaymentPaid_AndLateRunsCatchUp()
    {
        var booking = await BookAndPayBusinessAsync();

        // Five days before departure the Business band costs 15%, so only part of the payment comes back.
        var cancelledAt = await CancelAsync(booking.Pnr, new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc));
        var stored = (await _context.Bookings.GetByPnrAsync(booking.Pnr))!;

        // A run that comes late moves the refund straight from Pending to Completed.
        Assert.Equal(new RefundProcessingResult(0, 1), await CreateRefunds(cancelledAt.AddHours(3)).ProcessDueAsync());
        Assert.Equal(RefundStatus.Completed, stored.Cancellation!.Refunds.Single().Status);
        Assert.True(stored.Cancellation.Refunds.Single().Amount < stored.Payments.Single().Amount);
        Assert.Equal(PaymentStatus.Paid, stored.Payments.Single().Status);
    }

    [Fact]
    public async Task RefundsThatAreNotDue_AreNotTouched()
    {
        // Economy nine days out is inside the no-refund window.
        var booking = await BookAndPayAsync(BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103))));
        await CancelAsync(booking.Pnr, TestData.Now);

        Assert.Equal(new RefundProcessingResult(0, 0), await CreateRefunds(TestData.Now.AddDays(1)).ProcessDueAsync());
        Assert.Equal(RefundStatus.NotEligible, (await _context.Bookings.GetByPnrAsync(booking.Pnr))!.Cancellation!.Refunds.Single().Status);
    }

    [Fact]
    public async Task OneWayTrip_IsCompletedAfterLanding()
    {
        var booking = await BookAndPayBusinessAsync();
        var stored = (await _context.Bookings.GetByPnrAsync(booking.Pnr))!;

        Assert.Equal(new TripCompletionResult(0, 0, 0), await CreateCompletion(new DateTime(2026, 11, 10, 17, 0, 0, DateTimeKind.Utc)).CompleteFinishedAsync());

        var result = await CreateCompletion(new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc)).CompleteFinishedAsync();

        Assert.Equal(new TripCompletionResult(1, 1, 1), result);
        Assert.Equal(BookingStatus.Completed, stored.Status);
        Assert.Equal(TicketStatus.Used, stored.Passengers.Single().Tickets.Single().Status);
        Assert.Equal(FlightStatus.Completed, _context.Data.Outbound.Status);
        Assert.Equal(FlightStatus.Scheduled, _context.Data.Return.Status);
        Assert.Equal(BookingStatus.Completed, (await _context.CreateBookingService().GetMyBookingsAsync(1)).Single().Status);
    }

    [Fact]
    public async Task RoundTrip_IsCompletedOnlyWhenTheReturnHasLanded()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103), (2, 203)));
        request.TripType = TripType.RoundTrip;
        request.Legs.Add(new BookingLegRequestDto { FlightId = 2, CabinClass = CabinClass.Economy });
        var booking = await BookAndPayAsync(request);
        var stored = (await _context.Bookings.GetByPnrAsync(booking.Pnr))!;
        var tickets = stored.Passengers.Single().Tickets.OrderBy(t => t.BookingFlight.LegOrder).ToList();

        await CreateCompletion(new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc)).CompleteFinishedAsync();
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Equal(new[] { TicketStatus.Used, TicketStatus.Issued }, tickets.Select(t => t.Status));

        await CreateCompletion(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)).CompleteFinishedAsync();
        Assert.Equal(BookingStatus.Completed, stored.Status);
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Used, t.Status));
    }

    [Fact]
    public async Task CancelledBookingsAndFlights_StayAsTheyAre()
    {
        var booking = await BookAndPayBusinessAsync();
        await CancelAsync(booking.Pnr, TestData.Now);
        _context.Data.Return.Status = FlightStatus.Cancelled;

        await CreateCompletion(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)).CompleteFinishedAsync();

        var stored = (await _context.Bookings.GetByPnrAsync(booking.Pnr))!;
        Assert.Equal(BookingStatus.Cancelled, stored.Status);
        Assert.Equal(TicketStatus.Cancelled, stored.Passengers.Single().Tickets.Single().Status);
        Assert.Equal(FlightStatus.Cancelled, _context.Data.Return.Status);
        Assert.Equal(FlightStatus.Completed, _context.Data.Outbound.Status);
    }
}
