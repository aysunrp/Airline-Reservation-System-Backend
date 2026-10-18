using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Aircraft;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminCatalogEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminCatalogEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminEndpoints_NeedTheAdminRole()
    {
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/routes", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsBothTables()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var dashboard = await (await admin.GetAsync("/api/admin/dashboard")).ReadAsync<AdminDashboardDto>();

        Assert.Equal("AV 101", Assert.Single(dashboard.UpcomingFlights).FlightNumber);
    }

    [Fact]
    public async Task Aircraft_CrudCycle()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var request = new AircraftRequestDto
        {
            Name = "AEROVA Caspian", Model = "A321neo", RegistrationNumber = "4k-av14",
            BusinessSeats = 4, ComfortSeats = 0, EconomySeats = 12
        };

        var created = await admin.PostAsJsonAsync("/api/admin/aircraft", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var aircraft = await created.ReadAsync<AircraftDetailsDto>();
        Assert.Equal($"/api/admin/aircraft/{aircraft.Aircraft.Id}", created.Headers.Location?.AbsolutePath);
        Assert.Equal(16, aircraft.Aircraft.TotalSeats);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/aircraft", request, Json)).StatusCode);

        request.Status = AircraftStatus.Maintenance;
        var updated = await admin.PutAsJsonAsync($"/api/admin/aircraft/{aircraft.Aircraft.Id}", request, Json);
        Assert.Equal(AircraftStatus.Maintenance, (await updated.ReadAsync<AircraftDetailsDto>()).Aircraft.Status);

        Assert.Single(await (await admin.GetAsync("/api/admin/aircraft?search=caspian")).ReadAsync<List<AircraftDto>>());

        request.EconomySeats = 0;
        request.BusinessSeats = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/aircraft/{aircraft.Aircraft.Id}", request, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/aircraft/{aircraft.Aircraft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/aircraft/{aircraft.Aircraft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Routes_CreateUpdateAndInUseRules()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/routes", new RouteRequestDto
        {
            OriginCode = "LHR", DestinationCode = "GYD", DistanceKm = 3950, DurationMinutes = 330
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = await created.ReadAsync<RouteDto>();
        Assert.Equal("LHR-GYD", route.Code);

        var routes = await (await admin.GetAsync("/api/admin/routes")).ReadAsync<List<RouteDto>>();
        var seededRoute = routes.Single(r => r.Code == "GYD-LHR");

        // The seeded route carries a flight: it cannot be deleted, and creating it again conflicts.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/admin/routes/{seededRoute.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/routes", new RouteRequestDto
        {
            OriginCode = "GYD", DestinationCode = "LHR", DistanceKm = 3950, DurationMinutes = 340
        }, Json)).StatusCode);

        var deactivated = await admin.PutAsJsonAsync($"/api/admin/routes/{route.Id}", new RouteRequestDto
        {
            OriginCode = "LHR", DestinationCode = "GYD", DistanceKm = 3950, DurationMinutes = 330, IsActive = false
        }, Json);
        Assert.False((await deactivated.ReadAsync<RouteDto>()).IsActive);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/routes/{route.Id}")).StatusCode);
    }

    [Fact]
    public async Task Schedules_CreateFilterAndDelete()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var routeId = (await (await admin.GetAsync("/api/admin/routes?search=GYD-LHR")).ReadAsync<List<RouteDto>>()).Single().Id;

        var created = await admin.PostAsJsonAsync("/api/admin/schedules", new ScheduleRequestDto
        {
            FlightNumber = "av205", RouteId = routeId, DepartureTime = new TimeOnly(14, 20), ArrivalTime = new TimeOnly(19, 15),
            DaysOfWeek = ScheduleDays.Monday, ValidFrom = new DateOnly(2026, 11, 1), ValidTo = new DateOnly(2026, 12, 31)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var schedule = await created.ReadAsync<ScheduleDto>();
        Assert.Equal("AV 205", schedule.FlightNumber);

        Assert.Single(await (await admin.GetAsync("/api/admin/schedules?date=2026-11-02")).ReadAsync<List<ScheduleDto>>()); // Monday
        Assert.Empty(await (await admin.GetAsync("/api/admin/schedules?date=2026-11-03")).ReadAsync<List<ScheduleDto>>()); // Tuesday

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/schedules/{schedule.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/schedules/{schedule.Id}")).StatusCode);
    }
}
