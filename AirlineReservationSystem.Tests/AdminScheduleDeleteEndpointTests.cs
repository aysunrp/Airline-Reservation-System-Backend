using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Flight;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Deleting a schedule: allowed while it has created no flights, refused (without database detail) once it has.</summary>
public class AdminScheduleDeleteEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminScheduleDeleteEndpointTests(ApiFactory factory) => _factory = factory;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<int> CreateScheduleAsync(HttpClient admin, int routeId, int aircraftId, string flightNumber)
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

    [Fact]
    public async Task ASchedule_IsDeletedOnlyWhileItHasCreatedNoFlights_AndTheRestStaysInTheDatabase()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();
        var routeId = (await (await admin.GetAsync("/api/admin/routes?search=GYD-LHR")).ReadAsync<List<RouteDto>>()).Single().Id;
        var aircraftId = (await (await admin.GetAsync($"/api/flights/{seed.FlightId}")).ReadAsync<FlightResultDto>()).AircraftId;
        var unused = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 611");
        var used = await CreateScheduleAsync(admin, routeId, aircraftId, "AV 612");
        await admin.PostAsync($"/api/admin/schedules/{used}/generate?from={Today.AddDays(5):yyyy-MM-dd}&to={Today.AddDays(6):yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/schedules/{unused}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/schedules/{unused}")).StatusCode);

        var refused = await admin.DeleteAsync($"/api/admin/schedules/{used}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.DoesNotContain("FK_", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/schedules/{used}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/schedules/999999")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Flights.CountAsync(f => f.FlightScheduleId == used));
    }
}
