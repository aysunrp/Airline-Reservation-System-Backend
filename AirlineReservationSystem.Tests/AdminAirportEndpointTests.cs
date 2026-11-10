using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Airport;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminAirportEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminAirportEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static AirportRequestDto Tbilisi() => new()
    {
        Code = "tbs", Name = "Tbilisi International Airport", City = "Tbilisi", CountryCode = "ge", TimeZoneId = "Asia/Tbilisi"
    };

    [Fact]
    public async Task Airports_CrudCycle()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/airports", Tbilisi(), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var airport = await created.ReadAsync<AirportDto>();
        Assert.Equal(("TBS", "GE", "Asia/Tbilisi", true), (airport.Code, airport.CountryCode, airport.TimeZoneId, airport.IsActive));

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/airports/{airport.Id}")).StatusCode);
        Assert.Single(await (await admin.GetAsync("/api/admin/airports?search=tbilisi")).ReadAsync<List<AirportDto>>());

        // Deactivated airports leave the customer search form but stay in the admin list.
        var inactive = Tbilisi();
        inactive.Name = "Tbilisi Shota Rustaveli International Airport";
        inactive.IsActive = false;
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/airports/{airport.Id}", inactive, Json)).ReadAsync<AirportDto>();
        Assert.Equal((inactive.Name, false), (updated.Name, updated.IsActive));
        Assert.DoesNotContain(await (await _factory.CreateClient().GetAsync("/api/airports")).ReadAsync<List<AirportDto>>(), a => a.Code == "TBS");
        Assert.Single(await (await admin.GetAsync("/api/admin/airports?isActive=false")).ReadAsync<List<AirportDto>>(), a => a.Code == "TBS");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/airports/{airport.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/airports/{airport.Id}")).StatusCode);
    }

    [Fact]
    public async Task Airports_RulesAreEnforced()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();
        var baku = (await (await admin.GetAsync("/api/admin/airports?search=GYD")).ReadAsync<List<AirportDto>>()).Single();

        var duplicate = Tbilisi();
        duplicate.Code = "gyd";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/airports", duplicate, Json)).StatusCode);

        foreach (var zone in new[] { "Azerbaijan Standard Time", "Mars/Olympus", "" })
        {
            var badZone = Tbilisi();
            badZone.TimeZoneId = zone;
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/airports", badZone, Json)).StatusCode);
        }

        var badCodes = Tbilisi();
        badCodes.Code = "TB1";
        badCodes.CountryCode = "GEO";
        var invalid = await admin.PostAsJsonAsync("/api/admin/airports", badCodes, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("three-letter airport code", body);
        Assert.Contains("two-letter country code", body);

        // Baku is used by the seeded route and flight.
        var inUse = await admin.DeleteAsync($"/api/admin/airports/{baku.Id}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inUse.StatusCode);
        Assert.Contains("Set it to inactive", await inUse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/airports/999999", Tbilisi(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/airports/999999")).StatusCode);
    }
}
