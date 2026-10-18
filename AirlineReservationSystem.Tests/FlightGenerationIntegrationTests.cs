using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Flight generation against the real database: segments, fares and seats are stored together and a second run adds nothing.</summary>
public class FlightGenerationIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public FlightGenerationIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ScheduleWithAStop_GeneratesBookableFlights_Once()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var routeId = (await (await admin.GetAsync("/api/admin/routes?search=GYD-LHR")).ReadAsync<List<RouteDto>>()).Single().Id;
        var aircraftId = (await (await admin.GetAsync($"/api/flights/{seed.FlightId}")).ReadAsync<FlightResultDto>()).AircraftId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Airports.AnyAsync(a => a.Code == "IST"))
            {
                db.Airports.Add(new Airport { Code = "IST", Name = "Istanbul Airport", City = "Istanbul", CountryCode = "TR", TimeZoneId = "Europe/Istanbul" });
                await db.SaveChangesAsync();
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await admin.PostAsJsonAsync("/api/admin/schedules", new ScheduleRequestDto
        {
            FlightNumber = "AV 909", RouteId = routeId, DefaultAircraftId = aircraftId,
            DepartureTime = new TimeOnly(14, 20), ArrivalTime = new TimeOnly(19, 15),
            DaysOfWeek = ScheduleDays.Everyday, ValidFrom = today,
            Fares =
            {
                new ScheduleFareRequestDto { CabinClass = CabinClass.Economy, BasePrice = 365 },
                new ScheduleFareRequestDto { CabinClass = CabinClass.Business, BasePrice = 820 }
            },
            Stops = { new ScheduleStopRequestDto { SequenceNo = 1, AirportCode = "IST", ArrivalOffsetMinutes = 190, LayoverMinutes = 80 } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var scheduleId = (await created.ReadAsync<ScheduleDto>()).Id;

        var from = today.AddDays(3);
        var to = today.AddDays(5);
        FlightGenerationResultDto first, second;
        using (var scope = _factory.Services.CreateScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<IFlightGenerationService>().GenerateAsync(scheduleId, from, to);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            second = await scope.ServiceProvider.GetRequiredService<IFlightGenerationService>().GenerateAsync(scheduleId, from, to);
        }

        Assert.Equal(3, first.Created.Count);
        Assert.Empty(first.Skipped);
        Assert.Empty(second.Created);
        Assert.Equal(3, second.AlreadyExisting);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var flights = await db.Flights.Where(f => f.FlightScheduleId == scheduleId)
                .Include(f => f.Segments).Include(f => f.Fares).Include(f => f.Seats)
                .AsSplitQuery().ToListAsync();
            var seatsOnAircraft = await db.AircraftSeats.CountAsync(s => s.AircraftId == aircraftId);

            Assert.Equal(3, flights.Count);
            Assert.All(flights, f =>
            {
                Assert.Equal(2, f.Segments.Count);
                Assert.Equal(2, f.Fares.Count);
                Assert.Equal(seatsOnAircraft, f.Seats.Count);
            });
        }

        // The flight is served like any other: two legs via Istanbul, the schedule's prices, and a free seat map.
        var flightId = first.Created[0].FlightId;
        var details = await (await admin.GetAsync($"/api/flights/{flightId}")).ReadAsync<FlightResultDto>();
        Assert.Equal("IST", Assert.Single(details.Via).AirportCode);
        Assert.Equal(80, details.Via[0].LayoverMinutes);
        Assert.Equal(365m, details.Fares[CabinClass.Economy]);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/flights/{flightId}/seats?cabinClass=Economy")).StatusCode);
    }

    private async Task<(HttpClient Admin, int RouteId, int AircraftId)> PrepareAsync()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var routeId = (await (await admin.GetAsync("/api/admin/routes?search=GYD-LHR")).ReadAsync<List<RouteDto>>()).Single().Id;
        var aircraftId = (await (await admin.GetAsync($"/api/flights/{seed.FlightId}")).ReadAsync<FlightResultDto>()).AircraftId;
        return (admin, routeId, aircraftId);
    }

    private static async Task<int> CreateDailyScheduleAsync(HttpClient admin, int routeId, int aircraftId, string flightNumber)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/schedules", new ScheduleRequestDto
        {
            FlightNumber = flightNumber, RouteId = routeId, DefaultAircraftId = aircraftId,
            DepartureTime = new TimeOnly(9, 30), ArrivalTime = new TimeOnly(13, 10),
            DaysOfWeek = ScheduleDays.Everyday, ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            Fares = { new ScheduleFareRequestDto { CabinClass = CabinClass.Economy, BasePrice = 420 } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.ReadAsync<ScheduleDto>()).Id;
    }

    [Fact]
    public async Task TwoRunsAtTheSameTime_StoreEachDayOnce()
    {
        var (admin, routeId, aircraftId) = await PrepareAsync();
        var scheduleId = await CreateDailyScheduleAsync(admin, routeId, aircraftId, "AV 910");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (from, to) = (today.AddDays(20), today.AddDays(24));

        async Task<FlightGenerationResultDto> RunAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IFlightGenerationService>().GenerateAsync(scheduleId, from, to);
        }

        var results = await Task.WhenAll(RunAsync(), RunAsync());

        // Whatever the interleaving, every day is created by exactly one run; the other run sees it or loses the insert.
        Assert.Equal(5, results.Sum(r => r.Created.Count));
        Assert.All(results, r => Assert.Equal(5, r.Created.Count + r.AlreadyExisting + r.Conflicts.Count));
        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var days = await db.Flights.Where(f => f.FlightScheduleId == scheduleId).Select(f => f.ScheduledDate).ToListAsync();
        Assert.Equal(5, days.Count);
        Assert.Equal(5, days.Distinct().Count());
        Assert.Equal(5 * await db.AircraftSeats.CountAsync(s => s.AircraftId == aircraftId),
            await db.FlightSeats.CountAsync(s => s.Flight.FlightScheduleId == scheduleId));
    }

    [Fact]
    public async Task UniqueIndexes_RejectASecondFlightForTheDay_ButNotHandMadeFlights()
    {
        var (admin, routeId, aircraftId) = await PrepareAsync();
        var scheduleId = await CreateDailyScheduleAsync(admin, routeId, aircraftId, "AV 911");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var departure = day.ToDateTime(new TimeOnly(5, 30), DateTimeKind.Utc);

        Flight NewFlight(string number, DateTime departureAt, DateOnly? scheduledDate) => new()
        {
            FlightNumber = number, FlightScheduleId = scheduleId, ScheduledDate = scheduledDate, RouteId = routeId, AircraftId = aircraftId,
            DepartureAt = departureAt, ArrivalAt = departureAt.AddHours(7),
            Segments = { new FlightSegment { SequenceNo = 1, OriginAirportId = 0, DestinationAirportId = 0, DepartureAt = departureAt, ArrivalAt = departureAt.AddHours(7) } },
            Fares = { new FlightFare { CabinClass = CabinClass.Economy, BasePrice = 420 } }
        };

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var route = await db.Routes.SingleAsync(r => r.Id == routeId);
        var flights = scope.ServiceProvider.GetRequiredService<IFlightRepository>();
        Flight Valid(Flight f)
        {
            f.Segments.Single().OriginAirportId = route.OriginAirportId;
            f.Segments.Single().DestinationAirportId = route.DestinationAirportId;
            return f;
        }

        Assert.True(await flights.TryAddAsync(Valid(NewFlight("AV 911", departure, day))));

        // Same schedule and day at another time (e.g. after the schedule was moved): rejected, nothing stored.
        Assert.False(await flights.TryAddAsync(Valid(NewFlight("AV 911", departure.AddHours(1), day))));
        // Same number and departure: rejected by the older index.
        Assert.False(await flights.TryAddAsync(Valid(NewFlight("AV 911", departure, day.AddDays(1)))));

        // Flights made by hand have no scheduled date, so several of them can point at the same schedule.
        Assert.True(await flights.TryAddAsync(Valid(NewFlight("AV 912", departure, null))));
        Assert.True(await flights.TryAddAsync(Valid(NewFlight("AV 913", departure, null))));

        Assert.Equal(1, await db.Flights.CountAsync(f => f.FlightScheduleId == scheduleId && f.ScheduledDate == day));
        Assert.Equal(0, await db.Flights.CountAsync(f => f.FlightScheduleId == scheduleId && f.ScheduledDate == day.AddDays(1)));

        // Any other database error still surfaces: an unknown route breaks a foreign key.
        var broken = Valid(NewFlight("AV 914", departure, null));
        broken.RouteId = 999_999;
        await Assert.ThrowsAsync<DbUpdateException>(() => flights.TryAddAsync(broken));
    }

    [Fact]
    public async Task Search_UsesTheLocalDayAtTheOriginAirport()
    {
        var (admin, _, aircraftId) = await PrepareAsync();

        // 20:30 UTC is 00:30 the next day in Baku (UTC+4).
        var bakuDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);
        var departure = bakuDay.AddDays(-1).ToDateTime(new TimeOnly(20, 30), DateTimeKind.Utc);
        var created = await admin.PostAsJsonAsync("/api/admin/flights", new FlightRequestDto
        {
            FlightNumber = "AV 950", OriginCode = "GYD", DestinationCode = "LHR", AircraftId = aircraftId,
            DepartureAt = departure, ArrivalAt = departure.AddHours(7),
            Fares = new() { [CabinClass.Economy] = 300 }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        async Task<bool> FoundOnAsync(DateOnly date)
        {
            var response = await admin.PostAsJsonAsync("/api/flights/search", new
            {
                legs = new[] { new { originCode = "GYD", destinationCode = "LHR", date } }
            }, Json);
            var legs = await response.ReadAsync<List<FlightSearchLegResultDto>>();
            return legs.Single().Flights.Any(f => f.FlightNumber == "AV 950");
        }

        Assert.True(await FoundOnAsync(bakuDay));
        Assert.False(await FoundOnAsync(bakuDay.AddDays(-1)));
    }
}
