using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Destinations;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Adding a country, and a city with its first airport, from the admin panel: who may, what is refused, and that the seed leaves them alone.</summary>
public class DestinationCreateEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DestinationCreateEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = false;
    }

    private static object Country(string code, string name = "Norway") => new
    {
        code, name, nameAz = "Norveç", nameRu = "Норвегия",
        description = "Fjords and northern lights.", descriptionAz = "Fyordlar və şimal işığı.", descriptionRu = "Фьорды и северное сияние."
    };

    private static object City(string name, string airportCode, string timeZoneId = "Europe/Oslo") => new
    {
        name, nameAz = "Oslo", nameRu = "Осло", aliases = "Oslo; Христиания",
        description = "Capital on the fjord.", descriptionAz = "Fyord sahilində paytaxt.", descriptionRu = "Столица на фьорде.",
        airportCode, airportName = "Oslo Gardermoen Airport", timeZoneId
    };

    private async Task<HttpClient> AdminAsync() => (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

    private static async Task<DestinationCountryDto> CreateCountryAsync(HttpClient admin, string code, string name = "Norway")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/destinations/countries", Country(code, name), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<DestinationCountryDto>();
    }

    [Fact]
    public async Task OnlyAnAdmin_AddsACountry_AndItIsActiveAndLast()
    {
        var anonymous = _factory.CreateClient();
        var traveller = (await _factory.CreateAccountAsync(UserRole.User)).Client;
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/destinations/countries", Country("NO"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await traveller.PostAsJsonAsync("/api/admin/destinations/countries", Country("NO"), Json)).StatusCode);

        var before = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var created = await CreateCountryAsync(admin, "no");

        Assert.Equal("NO", created.Code);
        Assert.True(created.IsActive);
        Assert.Empty(created.Cities);
        Assert.Equal(before.Count == 0 ? 0 : before.Max(c => c.SortOrder) + 1, created.SortOrder);
        var after = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.Contains(after, c => c.Code == "NO" && c.NameAz == "Norveç");
    }

    [Fact]
    public async Task ACountry_IsRefused_ForABadCode_ADuplicate_AndARetiredCode()
    {
        var admin = await AdminAsync();
        await CreateCountryAsync(admin, "IS", "Iceland");

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/destinations/countries", Country("is", "Iceland"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/destinations/countries", Country("ISL"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/destinations/countries", Country("1A"), Json)).StatusCode);
        // The seed switches Qatar off at every start, so adding it would not last.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/destinations/countries", Country("QA", "Qatar"), Json)).StatusCode);

        var noText = new { code = "FI", name = "", nameAz = "Finlandiya", nameRu = "Финляндия", description = "x", descriptionAz = "x", descriptionRu = "x" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/destinations/countries", noText, Json)).StatusCode);
    }

    [Fact]
    public async Task ACity_IsAddedWithItsNewAirport_AndTheCountryComesBack()
    {
        var admin = await AdminAsync();
        var country = await CreateCountryAsync(admin, "SE", "Sweden");

        var response = await admin.PostAsJsonAsync($"/api/admin/destinations/countries/{country.Id}/cities", City("Stockholm", "ARN", "Europe/Stockholm"), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var updated = await response.ReadAsync<DestinationCountryDto>();

        var city = Assert.Single(updated.Cities);
        Assert.Equal("Stockholm", city.Name);
        Assert.True(city.IsActive);
        Assert.False(city.IsHub);
        Assert.Equal(new[] { "Oslo", "Христиания" }, city.Aliases);
        var airport = Assert.Single(city.Airports);
        Assert.Equal("ARN", airport.Code);
        Assert.Equal("Europe/Stockholm", airport.TimeZoneId);

        // The airport also exists on its own, under the country of the city.
        var airports = await (await admin.GetAsync("/api/admin/airports?search=ARN")).ReadAsync<List<Service.Helpers.DTOs.Airport.AirportDto>>();
        var stored = Assert.Single(airports, a => a.Code == "ARN");
        Assert.Equal("SE", stored.CountryCode);
        Assert.Equal("Stockholm", stored.City);

        // The public catalog shows it (a city is a destination once it is active).
        var catalog = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.Contains(catalog.Single(c => c.Code == "SE").Cities, c => c.Name == "Stockholm");
    }

    [Fact]
    public async Task ACity_IsRefused_ForAnUnknownCountry_ABadAirport_ADuplicateName_AndATakenAirport()
    {
        var admin = await AdminAsync();
        var country = await CreateCountryAsync(admin, "FI", "Finland");
        var url = $"/api/admin/destinations/countries/{country.Id}/cities";

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/destinations/countries/999999/cities", City("Helsinki", "HEL"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, City("Helsinki", "HE"), Json)).StatusCode);
        // A Windows time zone name is not an IANA zone, the same rule as for an airport created on its own.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, City("Helsinki", "HEL", "FLE Standard Time"), Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(url, City("Helsinki", "HEL", "Europe/Helsinki"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, City("helsinki", "HFS", "Europe/Helsinki"), Json)).StatusCode);
        // HEL already serves Helsinki; another city cannot take it over.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, City("Espoo", "HEL", "Europe/Helsinki"), Json)).StatusCode);
        // An airport the seed retires would be switched off again at the next start.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, City("Tampere", "SHJ", "Asia/Dubai"), Json)).StatusCode);
    }

    [Fact]
    public async Task AnExistingAirportWithoutACity_IsLinked_NotDuplicated_AndOneOfAnotherCountryIsRefused()
    {
        var admin = await AdminAsync();
        var country = await CreateCountryAsync(admin, "DK", "Denmark");

        var airport = new { code = "CPH", name = "Copenhagen Airport", city = "Copenhagen", countryCode = "DK", timeZoneId = "Europe/Copenhagen", isActive = true };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/airports", airport, Json)).StatusCode);
        var foreign = new { code = "BGO", name = "Bergen Airport", city = "Bergen", countryCode = "NO", timeZoneId = "Europe/Oslo", isActive = true };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/airports", foreign, Json)).StatusCode);

        var url = $"/api/admin/destinations/countries/{country.Id}/cities";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, City("Aarhus", "BGO"), Json)).StatusCode);

        var linked = await (await admin.PostAsJsonAsync(url, City("Copenhagen", "CPH", "Europe/Copenhagen"), Json)).ReadAsync<DestinationCountryDto>();
        Assert.Equal("CPH", Assert.Single(Assert.Single(linked.Cities).Airports).Code);
        var airports = await (await admin.GetAsync("/api/admin/airports?search=CPH")).ReadAsync<List<Service.Helpers.DTOs.Airport.AirportDto>>();
        Assert.Single(airports, a => a.Code == "CPH");
    }

    [Fact]
    public async Task WhatAnAdminAdded_StaysWhenTheSeedRunsAgain()
    {
        var admin = await AdminAsync();
        var country = await CreateCountryAsync(admin, "BE", "Belgium");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/admin/destinations/countries/{country.Id}/cities", City("Brussels", "BRU", "Europe/Brussels"), Json)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();
        }

        var after = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var belgium = after.Single(c => c.Code == "BE");
        Assert.True(belgium.IsActive);
        var brussels = Assert.Single(belgium.Cities);
        Assert.True(brussels.IsActive);
        Assert.Single(brussels.Airports, a => a.Code == "BRU" && a.IsActive);
    }
}
