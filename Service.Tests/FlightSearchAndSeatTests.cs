using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class FlightSearchAndSeatTests
{
    private readonly TestData _data = new();
    private readonly FixedTimeProvider _clock = new(TestData.Now);

    private FlightSearchService CreateSearch()
        => new(_data.Airports(), _data.Flights(), _data.FlightSeats(), new FlightSearchRequestValidator(_clock), _clock);

    private static FlightSearchLegDto Leg(string from, string to, int day) => new()
    {
        OriginCode = from,
        DestinationCode = to,
        Date = new DateOnly(2026, 11, day)
    };

    [Fact]
    public async Task RoundTripSearch_ReturnsFlightsPerLeg()
    {
        var result = await CreateSearch().SearchAsync(new FlightSearchRequestDto { Legs = { Leg("gyd", "LHR", 10), Leg("LHR", "GYD", 17) } });

        Assert.Equal(2, result.Count);
        var outbound = Assert.Single(result[0].Flights);
        Assert.Equal("AV 205", outbound.FlightNumber);
        Assert.Equal("Baku", outbound.OriginCity);
        Assert.Equal("London", outbound.DestinationCity);
        Assert.Equal(535, outbound.DurationMinutes);
        Assert.Equal(1, outbound.Stops);
        Assert.Equal(new FlightStopDto("IST", 80), Assert.Single(outbound.Via));
        Assert.Collection(outbound.Segments,
            s => Assert.Equal(("GYD", "IST", TestData.OutboundDeparture, TestData.OutboundDeparture.AddMinutes(170)), (s.OriginCode, s.DestinationCode, s.DepartureAt, s.ArrivalAt)),
            s => Assert.Equal((2, "IST", "LHR"), (s.SequenceNo, s.OriginCode, s.DestinationCode)));
        Assert.Equal(365m, outbound.Fares[CabinClass.Economy]);
        Assert.Equal("A350-900", outbound.AircraftModel);

        var back = Assert.Single(result[1].Flights);
        Assert.Equal(0, back.Stops);
        Assert.Empty(back.Via);
        var directLeg = Assert.Single(back.Segments);
        Assert.Equal((back.DepartureAt, back.ArrivalAt), (directLeg.DepartureAt, directLeg.ArrivalAt));
    }

    [Fact]
    public async Task Search_FiltersByCabinAndFreeSeats()
    {
        var service = CreateSearch();
        FlightSearchRequestDto Request(CabinClass? cabin, int passengers) => new()
        {
            Legs = { Leg("GYD", "LHR", 10) },
            CabinClass = cabin,
            Passengers = passengers
        };

        // The outbound flight has 3 Economy and 2 Business seats and sells no Comfort.
        Assert.Single((await service.SearchAsync(Request(CabinClass.Economy, 3)))[0].Flights);
        Assert.Empty((await service.SearchAsync(Request(CabinClass.Economy, 4)))[0].Flights);
        Assert.Empty((await service.SearchAsync(Request(CabinClass.Comfort, 1)))[0].Flights);
        Assert.Single((await service.SearchAsync(Request(null, 5)))[0].Flights);

        // A booked seat and a still-held seat no longer count; a seat whose hold ran out does.
        _data.Seat(101).Status = SeatStatus.Booked;
        Assert.Empty((await service.SearchAsync(Request(CabinClass.Business, 2)))[0].Flights);
        _data.Seat(103).Status = SeatStatus.Held;
        _data.Seat(103).HeldUntil = TestData.Now.AddMinutes(-1);
        _data.Seat(104).Status = SeatStatus.Held;
        _data.Seat(104).HeldUntil = TestData.Now.AddMinutes(5);
        Assert.Single((await service.SearchAsync(Request(CabinClass.Economy, 2)))[0].Flights);
        Assert.Empty((await service.SearchAsync(Request(CabinClass.Economy, 3)))[0].Flights);
    }

    [Fact]
    public async Task Search_ReturnsAircraftIdForAdminForms()
    {
        var flight = await CreateSearch().GetFlightAsync(1);

        Assert.Equal(1, flight.AircraftId);
    }

    [Fact]
    public async Task Search_InvalidPassengerCount_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateSearch().SearchAsync(new FlightSearchRequestDto { Legs = { Leg("GYD", "LHR", 10) }, Passengers = 0 }));
    }

    [Fact]
    public async Task Search_NoFlightsOnDate_ReturnsEmptyLeg()
    {
        var result = await CreateSearch().SearchAsync(new FlightSearchRequestDto { Legs = { Leg("GYD", "LHR", 11) } });

        Assert.Empty(Assert.Single(result).Flights);
    }

    [Fact]
    public async Task Search_UnknownAirport_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSearch().SearchAsync(new FlightSearchRequestDto { Legs = { Leg("GYD", "JFK", 10) } }));
    }

    [Fact]
    public async Task Search_InvalidLegs_ThrowValidation()
    {
        var service = CreateSearch();

        await Assert.ThrowsAsync<ValidationException>(() => service.SearchAsync(new FlightSearchRequestDto()));
        await Assert.ThrowsAsync<ValidationException>(() => service.SearchAsync(new FlightSearchRequestDto { Legs = { Leg("GYD", "GYD", 10) } }));
        await Assert.ThrowsAsync<ValidationException>(() => service.SearchAsync(new FlightSearchRequestDto
        {
            Legs = { new FlightSearchLegDto { OriginCode = "GYD", DestinationCode = "LHR", Date = new DateOnly(2026, 10, 31) } }
        }));
    }

    [Fact]
    public async Task GetFlight_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSearch().GetFlightAsync(99));
    }

    [Fact]
    public async Task SeatMap_ShowsAvailabilityIncludingExpiredHolds()
    {
        _data.Seat(101).Status = SeatStatus.Booked;
        _data.Seat(103).Status = SeatStatus.Held;
        _data.Seat(103).HeldUntil = TestData.Now.AddMinutes(5);   // still held
        _data.Seat(104).Status = SeatStatus.Held;
        _data.Seat(104).HeldUntil = TestData.Now.AddMinutes(-1);  // hold ran out
        var service = new SeatService(_data.Flights(), _data.FlightSeats(), _clock);

        var map = await service.GetSeatMapAsync(1);

        Assert.Equal(new[] { "1A", "1B", "11A", "11B", "11C" }, map.Seats.Select(s => s.Label));
        Assert.Equal(new[] { false, true, false, true, true }, map.Seats.Select(s => s.IsAvailable));

        var economy = await service.GetSeatMapAsync(1, CabinClass.Economy);
        Assert.Equal(3, economy.Seats.Count);
    }

    [Fact]
    public async Task SeatMap_UnknownFlight_ThrowsNotFound()
    {
        var service = new SeatService(_data.Flights(), _data.FlightSeats(), _clock);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetSeatMapAsync(99));
    }

    private async Task<int> CountAsync(string from, string to, DateOnly date)
        => (await CreateSearch().SearchAsync(new FlightSearchRequestDto { Legs = { new FlightSearchLegDto { OriginCode = from, DestinationCode = to, Date = date } } }))[0].Flights.Count;

    [Theory]
    [InlineData(9, 20, 30, 10)]   // 00:30 on 10 Nov in Baku (UTC+4), still 9 Nov in UTC
    [InlineData(10, 19, 59, 10)]  // 23:59 on 10 Nov in Baku
    [InlineData(10, 20, 0, 11)]   // 00:00 on 11 Nov in Baku, still 10 Nov in UTC
    public async Task SearchDate_IsTheDepartureDayAtTheOriginAirport(int utcDay, int utcHour, int utcMinute, int bakuDay)
    {
        _data.Outbound.DepartureAt = new DateTime(2026, 11, utcDay, utcHour, utcMinute, 0, DateTimeKind.Utc);

        Assert.Equal(1, await CountAsync("GYD", "LHR", new DateOnly(2026, 11, bakuDay)));
        Assert.Equal(0, await CountAsync("GYD", "LHR", new DateOnly(2026, 11, bakuDay == 10 ? 11 : 10)));
        Assert.Equal(0, await CountAsync("GYD", "LHR", new DateOnly(2026, 11, bakuDay == 10 ? 9 : 12)));
    }

    [Fact]
    public async Task SearchDate_FollowsSummerTimeAtTheOrigin()
    {
        // 23:30 UTC on 16 Jul 2027 is 00:30 on 17 Jul in London (UTC+1 in summer).
        _data.Return.DepartureAt = new DateTime(2027, 7, 16, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal(1, await CountAsync("LHR", "GYD", new DateOnly(2027, 7, 17)));
        Assert.Equal(0, await CountAsync("LHR", "GYD", new DateOnly(2027, 7, 16)));
    }
}
