using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Flight;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminGenerationEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminGenerationEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Range(int fromDays, int toDays) => $"from={Today.AddDays(fromDays):yyyy-MM-dd}&to={Today.AddDays(toDays):yyyy-MM-dd}";

    private async Task<(HttpClient Admin, int RouteId, int AircraftId)> PrepareAsync()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var routeId = (await (await admin.GetAsync("/api/admin/routes?search=GYD-LHR")).ReadAsync<List<RouteDto>>()).Single().Id;
        var aircraftId = (await (await admin.GetAsync($"/api/flights/{seed.FlightId}")).ReadAsync<FlightResultDto>()).AircraftId;
        return (admin, routeId, aircraftId);
    }

    private static async Task<int> CreateScheduleAsync(HttpClient admin, int routeId, int? aircraftId, string flightNumber)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/schedules", new ScheduleRequestDto
        {
            FlightNumber = flightNumber, RouteId = routeId, DefaultAircraftId = aircraftId,
            DepartureTime = new TimeOnly(9, 30), ArrivalTime = new TimeOnly(13, 10),
            DaysOfWeek = ScheduleDays.Everyday, ValidFrom = Today,
            Fares = { new ScheduleFareRequestDto { CabinClass = CabinClass.Economy, BasePrice = 420 } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<ScheduleDto>()).Id;
    }

    private async Task<int> FlightCountAsync(int scheduleId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Flights.CountAsync(f => f.FlightScheduleId == scheduleId);
    }

    [Fact]
    public async Task GenerateEndpoints_NeedTheAdminRole()
    {
        await _factory.GetSeedAsync();
        var anonymous = _factory.CreateClient();
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);

        foreach (var url in new[] { "/api/admin/schedules/generate", "/api/admin/schedules/1/generate" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(url, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(url, null)).StatusCode);
        }
    }

    [Fact]
    public async Task OneSchedule_IsGeneratedOnce()
    {
        var (admin, routeId, aircraftId) = await PrepareAsync();
        var scheduleId = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 601");
        var url = $"/api/admin/schedules/{scheduleId}/generate?{Range(5, 7)}";

        var first = await admin.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.ReadAsync<FlightGenerationResultDto>();
        Assert.Equal(new[] { Today.AddDays(5), Today.AddDays(6), Today.AddDays(7) }, result.Created.Select(c => c.ScheduledDate));
        Assert.All(result.Created, c => Assert.Equal((scheduleId, "AV 601"), (c.ScheduleId, c.FlightNumber)));
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Conflicts);

        var again = await (await admin.PostAsync(url, null)).ReadAsync<FlightGenerationResultDto>();
        Assert.Empty(again.Created);
        Assert.Equal(3, again.AlreadyExisting);
        Assert.Equal(3, await FlightCountAsync(scheduleId));
    }

    [Fact]
    public async Task AllSchedules_AreGenerated_AndUnusableOnesAreListed()
    {
        var (admin, routeId, aircraftId) = await PrepareAsync();
        var first = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 611");
        var second = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 612");
        var withoutAircraft = await CreateScheduleAsync(admin, routeId, null, "AV 613");
        await admin.PostAsync($"/api/admin/schedules/{first}/generate?{Range(10, 11)}", null);

        var response = await admin.PostAsync($"/api/admin/schedules/generate?{Range(10, 11)}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<FlightGenerationResultDto>();
        Assert.Equal(2, result.Created.Count(c => c.ScheduleId == second));
        Assert.DoesNotContain(result.Created, c => c.ScheduleId == first);
        Assert.True(result.AlreadyExisting >= 2);
        var skipped = Assert.Single(result.Skipped, s => s.ScheduleId == withoutAircraft);
        Assert.Equal("The schedule has no aircraft assigned.", skipped.Reason);
        Assert.Equal(2, await FlightCountAsync(first));
        Assert.Equal(2, await FlightCountAsync(second));
    }

    [Fact]
    public async Task ParallelRequests_DoNotDuplicateFlights()
    {
        var (admin, routeId, aircraftId) = await PrepareAsync();
        var scheduleId = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 621");
        var url = $"/api/admin/schedules/{scheduleId}/generate?{Range(20, 23)}";

        var responses = await Task.WhenAll(admin.PostAsync(url, null), admin.PostAsync(url, null));
        var results = await Task.WhenAll(responses.Select(r => r.ReadAsync<FlightGenerationResultDto>()));

        // Each day is created by one request; the other one finds it or reports the lost insert as a conflict.
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(4, results.Sum(r => r.Created.Count));
        Assert.All(results, r => Assert.Equal(4, r.Created.Count + r.AlreadyExisting + r.Conflicts.Count));
        Assert.Equal(4, await FlightCountAsync(scheduleId));
    }

    [Fact]
    public async Task BadInput_IsRejected()
    {
        var (admin, _, _) = await PrepareAsync();

        var backwards = await admin.PostAsync($"/api/admin/schedules/generate?{Range(10, 5)}", null);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains("The end date cannot be before the start date.", await backwards.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/admin/schedules/generate?{Range(0, 400)}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/schedules/generate?from=not-a-date", null)).StatusCode);

        var missing = await admin.PostAsync("/api/admin/schedules/999999/generate", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Schedule not found.", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/schedules/abc/generate", null)).StatusCode);
    }
}
