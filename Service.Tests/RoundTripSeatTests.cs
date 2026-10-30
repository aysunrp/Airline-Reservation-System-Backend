using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;

namespace Service.Tests;

/// <summary>
/// A round trip is two flights with their own seat maps: every passenger has one seat on each flight, each seat is checked and held
/// on its own flight, the outbound seat never stands in for the return one, and the same seat number may be chosen on both flights.
/// Outbound flight 1 (AV 205) has seats 101-105, return flight 2 (AV 102) seats 201-205; 103/203 are both 11A, 104/204 both 11B.
/// </summary>
public class RoundTripSeatTests
{
    private readonly BookingTestContext _context = new();

    private static CreateBookingRequestDto RoundTrip(params BookingPassengerRequestDto[] passengers) => new()
    {
        TripType = TripType.RoundTrip,
        Legs =
        {
            new BookingLegRequestDto { FlightId = 1, CabinClass = CabinClass.Economy },
            new BookingLegRequestDto { FlightId = 2, CabinClass = CabinClass.Economy }
        },
        Passengers = passengers.ToList(),
        ContactEmail = "a@b.com",
        ContactPhone = "+994501234567"
    };

    private static BookingPassengerRequestDto Passenger(string name, params (int FlightId, int SeatId)[] seats)
        => BookingTestContext.Passenger(name, 1990, seats);

    private Task<BookingDto> BookAsync(CreateBookingRequestDto request) => _context.CreateBookingService().CreateBookingAsync(null, request);

    [Fact]
    public async Task EachPassenger_KeepsTheirOwnSeatOnEachFlight()
    {
        var booking = await BookAsync(RoundTrip(
            Passenger("Aysun", (1, 103), (2, 204)),
            Passenger("Orkhan", (1, 104), (2, 203))));

        var seats = booking.Passengers.Select(p => p.Tickets.OrderBy(t => t.LegOrder).Select(t => t.Seat).ToArray()).ToArray();
        Assert.Equal(new[] { "11A", "11B" }, seats[0]);
        Assert.Equal(new[] { "11B", "11A" }, seats[1]);

        // Held on the flight they belong to, and nothing else.
        foreach (var id in new[] { 103, 104, 203, 204 })
        {
            Assert.Equal(SeatStatus.Held, _context.Data.Seat(id).Status);
        }
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(105).Status);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(205).Status);
    }

    [Fact]
    public async Task TheSameSeatNumber_MayBeChosenOnBothFlights()
    {
        var booking = await BookAsync(RoundTrip(Passenger("Aysun", (1, 103), (2, 203))));

        Assert.Equal(new[] { "11A", "11A" }, booking.Passengers.Single().Tickets.OrderBy(t => t.LegOrder).Select(t => t.Seat));
        Assert.Equal(SeatStatus.Held, _context.Data.Seat(103).Status);
        Assert.Equal(SeatStatus.Held, _context.Data.Seat(203).Status);
    }

    [Fact]
    public async Task TheOutboundSeat_DoesNotCountForTheReturnFlight()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => BookAsync(RoundTrip(Passenger("Aysun", (1, 103)))));

        Assert.Contains("AV 102", error.Message);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Empty(await _context.Bookings.GetAllAsync());
    }

    [Fact]
    public async Task ASeatOfTheOutboundFlight_CannotBeUsedOnTheReturnFlight()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => BookAsync(RoundTrip(Passenger("Aysun", (1, 103), (2, 104)))));

        Assert.Contains("AV 102 nömrəli uçuşa aid deyil", error.Message);
        Assert.Empty(await _context.Bookings.GetAllAsync());
    }

    [Fact]
    public async Task TwoPassengers_CannotShareASeatOnOneFlight_EvenWithDifferentSeatsOnTheOther()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => BookAsync(RoundTrip(
            Passenger("Aysun", (1, 103), (2, 203)),
            Passenger("Orkhan", (1, 104), (2, 203)))));

        Assert.Empty(await _context.Bookings.GetAllAsync());
    }

    [Fact]
    public async Task ASeatTakenOnTheReturnFlight_IsRefused_WhileTheSameNumberOnTheOutboundFlightStaysBookable()
    {
        // Somebody else holds 11A on the way back (203) but not on the way out.
        await BookAsync(RoundTrip(Passenger("Orkhan", (1, 105), (2, 203))));

        var taken = await Assert.ThrowsAsync<ConflictException>(() => BookAsync(RoundTrip(Passenger("Aysun", (1, 103), (2, 203)))));
        Assert.Contains("AV 102 nömrəli uçuşda 11A", taken.Message);

        // The refused booking held nothing: the outbound seat it asked for is still free.
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Single(await _context.Bookings.GetAllAsync());

        // 11A outbound is still free, so with another return seat the same traveller books.
        var booking = await BookAsync(RoundTrip(Passenger("Aysun", (1, 103), (2, 204))));
        Assert.Equal(new[] { "11A", "11B" }, booking.Passengers.Single().Tickets.OrderBy(t => t.LegOrder).Select(t => t.Seat));
    }

    [Fact]
    public async Task EachFlight_IsPricedOnItsOwn()
    {
        var booking = await BookAsync(RoundTrip(Passenger("Aysun", (1, 103), (2, 203))));

        // Economy 365 out and 420 back, each on its own ticket.
        Assert.Equal(new[] { 365m, 420m }, booking.Passengers.Single().Tickets.OrderBy(t => t.LegOrder).Select(t => t.FareAmount));
        Assert.Equal(785m, booking.Price.BaseFare);
    }
}
