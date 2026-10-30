using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.DTOs.Flight;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The destination catalog in the database: seed, public API, admin switches, search selectors and voice search.</summary>
public class DestinationCatalogEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DestinationCatalogEndpointTests(ApiFactory factory) => _factory = factory;

    private async Task<DestinationSeedResultDto> SeedAsync()
    {
        await _factory.GetSeedAsync(); // the test network (GYD and LHR exist before the catalog does)
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();
    }

    private async Task<List<DestinationCountryDto>> PublicAsync()
        => await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();

    [Fact]
    public async Task RunningTheSeedAgain_ChangesNothing_AndTheExistingAirportsKeepOneRecordEach()
    {
        await SeedAsync();
        var again = await SeedAsync();

        Assert.Equal((0, 0, 0, 0), (again.CountriesAdded, again.CitiesAdded, again.AirportsAdded, again.AirportsLinked));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(20, await db.Countries.CountAsync());
        Assert.Equal(37, await db.DestinationCities.CountAsync());
        Assert.Equal(38, await db.Airports.CountAsync());
        Assert.Equal(1, await db.Airports.CountAsync(a => a.Code == "GYD")); // the airport that existed first was linked, not duplicated
        Assert.Equal("Baku", (await db.Airports.Include(a => a.CityRecord).SingleAsync(a => a.Code == "GYD")).CityRecord!.Name);
        Assert.Equal("London", (await db.Airports.Include(a => a.CityRecord).SingleAsync(a => a.Code == "LHR")).CityRecord!.Name);
    }

    [Fact]
    public async Task TheSeed_CreatesNoRouteScheduleOrFlight()
    {
        await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Only the test network's own route exists; the 36 new airports have none.
        var newAirportIds = await db.Airports.Where(a => a.Code != "GYD" && a.Code != "LHR").Select(a => a.Id).ToListAsync();
        Assert.False(await db.Routes.AnyAsync(r => newAirportIds.Contains(r.OriginAirportId) || newAirportIds.Contains(r.DestinationAirportId)));
    }

    [Fact]
    public async Task ThePublicCatalog_HasTheTwentyCountries_WithCitiesAirportsAndWhetherTheyHaveRoutes()
    {
        await SeedAsync();

        var catalog = await PublicAsync();

        Assert.Equal(new[] { "AZ", "TR", "AE", "GB", "FR", "DE", "IT", "ES", "GE", "QA", "SA", "CH", "NL", "US", "CA", "JP", "CN", "SG", "TH", "MV" }, catalog.Select(c => c.Code));
        Assert.Equal(37, catalog.Sum(c => c.Cities.Count));
        Assert.Equal(38, catalog.SelectMany(c => c.Cities).Sum(city => city.Airports.Count));
        var japan = catalog.Single(c => c.Code == "JP");
        Assert.Equal(("Tokio", "Токио"), (japan.Cities[0].NameAz, japan.Cities[0].NameRu));
        Assert.Equal(new[] { "HND", "NRT" }, japan.Cities[0].Airports.Select(a => a.Code));
        Assert.Contains("Tokyo", japan.Cities[0].Aliases);

        var airports = catalog.SelectMany(c => c.Cities).SelectMany(c => c.Airports).ToDictionary(a => a.Code);
        Assert.True(airports["GYD"].HasRoutes); // the test network flies from Baku
        var baku = catalog.Single(c => c.Code == "AZ").Cities.Single(c => c.Name == "Baku");
        Assert.True(baku.IsHub); // Baku stays in the catalog (origin, airports) and is marked as the home base
        Assert.Single(catalog.SelectMany(c => c.Cities), c => c.IsHub);
        Assert.False(airports["NRT"].HasRoutes); // a catalog airport nobody flies to yet
    }

    [Fact]
    public async Task EveryCatalogAirport_IsOfferedByTheSearchSelectors()
    {
        await SeedAsync();

        var airports = await (await _factory.CreateClient().GetAsync("/api/airports")).ReadAsync<List<AirportDto>>();

        Assert.All(new[] { "NRT", "BCN", "MLE", "YUL", "JED", "GNJ", "SIN", "KIX" }, code => Assert.Contains(airports, a => a.Code == code));
        Assert.Equal(airports.Count, airports.Select(a => a.Code).Distinct().Count());
    }

    [Fact]
    public async Task ADestinationWithoutRoutes_SearchesToAnHonestEmptyResult()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var request = new FlightSearchRequestDto { Legs = { new FlightSearchLegDto { OriginCode = "GYD", DestinationCode = "BCN", Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) } } };
        var response = await client.PostAsJsonAsync("/api/flights/search", request, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadAsync<List<FlightSearchLegResultDto>>()).Single().Flights);

        // The same search on a route that does have flights still finds them: the catalog changed nothing for existing searches.
        var real = new FlightSearchRequestDto { Legs = { new FlightSearchLegDto { OriginCode = "GYD", DestinationCode = "LHR", Date = DateOnly.FromDateTime((await _factory.GetSeedAsync()).Departure) } } };
        Assert.NotEmpty((await (await client.PostAsJsonAsync("/api/flights/search", real, Json)).ReadAsync<List<FlightSearchLegResultDto>>()).Single().Flights);
    }

    [Fact]
    public async Task OnlyAnAdmin_SwitchesCountriesAndCitiesOnAndOff_AndNothingIsDeleted()
    {
        await SeedAsync();
        var catalog = await PublicAsync();
        var azerbaijan = catalog.Single(c => c.Code == "AZ");
        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/destinations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/destinations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync($"/api/admin/destinations/countries/{azerbaijan.Id}/active", new { isActive = false }, Json)).StatusCode);

        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var off = await admin.PutAsJsonAsync($"/api/admin/destinations/countries/{azerbaijan.Id}/active", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.ReadAsync<DestinationCountryDto>()).IsActive);

        Assert.Equal(19, (await PublicAsync()).Count); // gone from the public catalog...
        var all = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.Equal(20, all.Count); // ...but still there for the admin
        Assert.Contains((await (await _factory.CreateClient().GetAsync("/api/airports")).ReadAsync<List<AirportDto>>()), a => a.Code == "GYD"); // its airports and flights are untouched

        var ganja = azerbaijan.Cities.Single(c => c.Name == "Ganja");
        await admin.PutAsJsonAsync($"/api/admin/destinations/countries/{azerbaijan.Id}/active", new { isActive = true }, Json);
        await admin.PutAsJsonAsync($"/api/admin/destinations/cities/{ganja.Id}/active", new { isActive = false }, Json);
        Assert.DoesNotContain((await PublicAsync()).Single(c => c.Code == "AZ").Cities, c => c.Name == "Ganja");
        await admin.PutAsJsonAsync($"/api/admin/destinations/cities/{ganja.Id}/active", new { isActive = true }, Json);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/destinations/countries/999999/active", new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/destinations/cities/999999/active", new { isActive = false }, Json)).StatusCode);
    }

    [Theory]
    [InlineData("Osaka'dan Barselonaya uçuşları göstər.", "az")]
    [InlineData("Show flights from Osaka to Barcelona.", "en")]
    [InlineData("Покажи рейсы из Осаки в Барселону.", "ru")]
    public async Task VoiceSearch_UnderstandsTheNewCities_InEveryLanguage(string text, string language)
    {
        await SeedAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/voice-search/interpret", new { text, language }, Json);
        var result = await response.ReadAsync<VoiceSearchInterpretationDto>();

        Assert.Equal(("KIX", "BCN"), (result.Origin!.Code, result.Destination!.Code));
    }

    [Fact]
    public async Task VoiceSearch_KnowsTokyoAsTokyoTokioAndТокио_AndAsksWhichOfItsTwoAirports()
    {
        await SeedAsync();

        foreach (var text in new[] { "Flights from Tokyo to Barcelona", "Tokiodan Barselonaya uçuşlar", "Рейсы из Токио в Барселону" })
        {
            var result = await (await _factory.CreateClient().PostAsJsonAsync("/api/voice-search/interpret", new { text }, Json)).ReadAsync<VoiceSearchInterpretationDto>();
            var issue = Assert.Single(result.Issues, i => i.Code == "ambiguous_origin");
            Assert.Equal(new[] { "HND", "NRT" }, issue.Options!.Select(o => o.Code).OrderBy(c => c));
        }
    }
}


/// <summary>The first run of the seed on a database that has only the test network: exact counts.</summary>
public class DestinationCatalogFirstRunTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DestinationCatalogFirstRunTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TheFirstRun_AddsTheCatalog_AndLinksTheTwoAirportsThatAlreadyExisted()
    {
        await _factory.GetSeedAsync(); // GYD and LHR exist before the catalog does
        using var scope = _factory.Services.CreateScope();

        var first = await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();

        Assert.Equal(20, first.CountriesAdded);
        Assert.Equal(37, first.CitiesAdded);
        Assert.Equal(36, first.AirportsAdded); // 38 catalog airports, GYD and LHR were already there
        Assert.Equal(2, first.AirportsLinked);
    }
}
