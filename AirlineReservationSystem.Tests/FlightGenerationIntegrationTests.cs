using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
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
}
