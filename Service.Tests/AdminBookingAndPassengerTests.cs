using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

public class AdminBookingAndPassengerTests
{
    private readonly BookingTestContext _context = new();

    private AdminBookingService CreateBookingService() => new(_context.Bookings, _context.CreateCancellationService());

    private AdminPassengerService CreatePassengerService() => new(new FakeBookingPassengerRepository(_context.Bookings));

    private async Task<BookingDto> BookAsync(string firstName, int seatId, bool pay, string lastName = "Panahli", string document = "C01234567")
    {
        var passenger = BookingTestContext.Passenger(firstName, 1990, (1, seatId));
        passenger.LastName = lastName;
        passenger.DocumentNumber = document;
        var booking = await _context.CreateBookingService().CreateBookingAsync(1, BookingTestContext.OneWay(passenger));
        return pay ? await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr)) : booking;
    }

    [Fact]
    public async Task BookingList_ShowsFirstFlightDetailsAndFilters()
    {
        var paid = await BookAsync("Aysun", 103, pay: true);
        await BookAsync("Orkhan", 104, pay: false, lastName: "Aliyev", document: "C07654321");
        var service = CreateBookingService();

        var all = await service.GetAllAsync();
        Assert.Equal(2, all.Count);

        var row = all.Single(r => r.Pnr == paid.Pnr);
        Assert.Equal("Aysun Panahli", row.LeadPassenger);
        Assert.Equal("AV 205", row.FlightNumber);
        Assert.Equal("Baku → London", row.Route);
        Assert.Equal(CabinClass.Economy, row.CabinClass);
        Assert.Equal(new[] { "11A" }, row.Seats);
        Assert.Equal(BookingStatus.Confirmed, row.Status);

        Assert.Single(await service.GetAllAsync(pnr: paid.Pnr[..3].ToLowerInvariant()));
        Assert.Single(await service.GetAllAsync(passengerName: "aliyev"));
        Assert.Single(await service.GetAllAsync(status: BookingStatus.Pending));
    }

    [Fact]
    public async Task AdminCancel_DoesNotNeedLastName_AndFollowsFareRules()
    {
        var paid = await BookAsync("Aysun", 103, pay: true);
        var service = CreateBookingService();

        var cancelled = await service.CancelAsync(paid.Pnr.ToLowerInvariant(), "Requested by phone");

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(RefundStatus.NotEligible, cancelled.Cancellation!.RefundStatus); // 9 days before an Economy flight
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(paid.Pnr));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync("ZZZZZZ"));
    }

    [Fact]
    public async Task Passengers_AreGroupedByDocumentWithTripCounts()
    {
        await BookAsync("Aysun", 103, pay: true);
        await BookAsync("Aysun", 104, pay: true);
        await BookAsync("Orkhan", 105, pay: false, lastName: "Aliyev", document: "C07654321");
        var service = CreatePassengerService();

        var passengers = await service.GetAllAsync();
        Assert.Equal(2, passengers.Count);

        var aysun = passengers.Single(p => p.DocumentNumber == "C01234567");
        Assert.Equal("Aysun Panahli", aysun.FullName);
        Assert.Equal("aysun@example.com", aysun.Email);
        Assert.Equal(2, aysun.Trips);
        Assert.True(aysun.IsActive);

        var orkhan = passengers.Single(p => p.DocumentNumber == "C07654321");
        Assert.Equal(0, orkhan.Trips);
        Assert.False(orkhan.IsActive);

        Assert.Single(await service.GetAllAsync(isActive: false));
        Assert.Single(await service.GetAllAsync(search: "c0123"));

        var details = await service.GetDetailsAsync(DocumentType.Passport, "c01234567");
        Assert.Equal(2, details.Bookings.Count);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(DocumentType.Passport, "X0000000"));
    }

    [Fact]
    public async Task Dashboard_ShowsRecentBookingsAndUpcomingFlights()
    {
        await BookAsync("Aysun", 103, pay: true);
        var service = new AdminDashboardService(_context.Bookings, _context.Data.Flights(), _context.Clock);

        var dashboard = await service.GetAsync();

        Assert.Single(dashboard.RecentBookings);
        Assert.Equal(new[] { "AV 205", "AV 102" }, dashboard.UpcomingFlights.Select(f => f.FlightNumber));
    }
}
