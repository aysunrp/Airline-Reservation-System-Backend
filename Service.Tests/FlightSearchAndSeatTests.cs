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
        => new(_data.Airports(), _data.Flights(), new FlightSearchRequestValidator(_clock));

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
        Assert.Equal(365m, outbound.Fares[CabinClass.Economy]);
        Assert.Equal("A350-900", outbound.AircraftModel);

        var back = Assert.Single(result[1].Flights);
        Assert.Equal(0, back.Stops);
        Assert.Empty(back.Via);
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
}
