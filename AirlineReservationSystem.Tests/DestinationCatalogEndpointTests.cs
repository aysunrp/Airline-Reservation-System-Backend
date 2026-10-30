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
        Assert.Equal(39, await db.Airports.CountAsync());
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

        // Only the test network's own route exists; the 37 new airports have none.
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
        Assert.Equal(39, catalog.SelectMany(c => c.Cities).Sum(city => city.Airports.Count));
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

    [Fact]
    public async Task ACityPage_HasItsOwnAirports_ItsCountry_AndTheOtherCitiesOfThatCountryOnly()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var catalog = await PublicAsync();
        var turkey = catalog.Single(c => c.Code == "TR");
        var istanbul = turkey.Cities.Single(c => c.Name == "Istanbul");

        var page = await (await client.GetAsync($"/api/destinations/cities/{istanbul.Id}")).ReadAsync<CityDestinationDto>();

        Assert.Equal("Istanbul", page.City.Name);
        Assert.Equal("turkey-istanbul.jpg", page.City.ImageFile);
        Assert.False(string.IsNullOrWhiteSpace(page.City.DescriptionAz));
        Assert.Equal(("TR", "Turkey", "Türkiyə"), (page.Country.Code, page.Country.Name, page.Country.NameAz));
        // Only Istanbul's airports are Istanbul's; Ankara and Antalya are other cities of Turkey, with their own airports.
        Assert.Equal(new[] { "IST", "SAW" }, page.City.Airports.Select(a => a.Code).OrderBy(c => c));
        Assert.Equal(new[] { "Ankara", "Antalya" }, page.OtherCities.Select(c => c.Name));
        Assert.Equal(new[] { "ESB" }, page.OtherCities.Single(c => c.Name == "Ankara").Airports.Select(a => a.Code));
        Assert.All(page.OtherCities, c => Assert.False(string.IsNullOrEmpty(c.ImageFile)));
        Assert.DoesNotContain(page.OtherCities, c => c.Id == istanbul.Id);

        // Every country works the same way: another country, another set of sister cities.
        var france = catalog.Single(c => c.Code == "FR");
        var nice = await (await client.GetAsync($"/api/destinations/cities/{france.Cities.Single(c => c.Name == "Nice").Id}")).ReadAsync<CityDestinationDto>();
        Assert.Equal("FR", nice.Country.Code);
        Assert.Equal(new[] { "Paris" }, nice.OtherCities.Select(c => c.Name));
        var qatar = await (await client.GetAsync($"/api/destinations/cities/{catalog.Single(c => c.Code == "QA").Cities.Single().Id}")).ReadAsync<CityDestinationDto>();
        Assert.Empty(qatar.OtherCities); // a one-city country has no sister cities
    }

    [Fact]
    public async Task ACityPage_IsNotFound_ForTheHomeBase_ASwitchedOffCityOrCountry_OrAnUnknownId()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var catalog = await PublicAsync();
        var baku = catalog.Single(c => c.Code == "AZ").Cities.Single(c => c.Name == "Baku");
        var ganja = catalog.Single(c => c.Code == "AZ").Cities.Single(c => c.Name == "Ganja");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/destinations/cities/{baku.Id}")).StatusCode); // a place to fly from
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/destinations/cities/{baku.Id}/flights")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/destinations/cities/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/destinations/cities/{ganja.Id}")).StatusCode);

        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        await admin.PutAsJsonAsync($"/api/admin/destinations/cities/{ganja.Id}/active", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/destinations/cities/{ganja.Id}")).StatusCode);
        await admin.PutAsJsonAsync($"/api/admin/destinations/cities/{ganja.Id}/active", new { isActive = true }, Json);

        var japan = catalog.Single(c => c.Code == "JP");
        await admin.PutAsJsonAsync($"/api/admin/destinations/countries/{japan.Id}/active", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/destinations/cities/{japan.Cities[0].Id}")).StatusCode);
        await admin.PutAsJsonAsync($"/api/admin/destinations/countries/{japan.Id}/active", new { isActive = true }, Json);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/destinations/cities/{japan.Cities[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task TheFlightsOfACity_AreTheRealFlightsFromBakuToItsAirports_AndNothingElse()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var catalog = await PublicAsync();
        var london = catalog.Single(c => c.Code == "GB").Cities.Single(c => c.Name == "London");
        var manchester = catalog.Single(c => c.Code == "GB").Cities.Single(c => c.Name == "Manchester");

        var flights = await (await client.GetAsync($"/api/destinations/cities/{london.Id}/flights")).ReadAsync<List<DestinationFlightDto>>();

        Assert.NotEmpty(flights); // the test network flies Baku -> London
        Assert.All(flights, f => Assert.Equal(("GYD", "LHR"), (f.OriginCode, f.DestinationCode)));
        Assert.Equal(flights.OrderBy(f => f.DepartureAt).Select(f => f.Id), flights.Select(f => f.Id));
        Assert.All(flights, f => Assert.True(f.DepartureAt >= DateTime.UtcNow.AddMinutes(-1)));
        Assert.All(flights, f => Assert.Equal(420m, f.LowestFare)); // the lowest base fare stored for the flight

        // A city of the same country with no route has no flights: nothing is borrowed from London.
        Assert.Empty(await (await client.GetAsync($"/api/destinations/cities/{manchester.Id}/flights")).ReadAsync<List<DestinationFlightDto>>());
    }

    [Fact]
    public async Task ThePickerAirports_CarryTheirCityAndCountryFromTheCatalog_NotFromTheBrowser()
    {
        await SeedAsync();

        var airports = await (await _factory.CreateClient().GetAsync("/api/airports")).ReadAsync<List<AirportDto>>();

        var istanbul = airports.Single(a => a.Code == "IST");
        Assert.Equal(("Turkey", "Türkiyə", "Турция"), (istanbul.CountryName, istanbul.CountryNameAz, istanbul.CountryNameRu));
        Assert.Equal(("İstanbul", "Стамбул"), (istanbul.CityNameAz, istanbul.CityNameRu));
        Assert.Equal("Antalya", airports.Single(a => a.Code == "AYT").City);
        Assert.Equal("Turkey", airports.Single(a => a.Code == "ESB").CountryName); // Ankara, from its city record
        Assert.Equal("United Kingdom", airports.Single(a => a.Code == "LHR").CountryName);
        Assert.Contains("Tokyo", airports.Single(a => a.Code == "NRT").Aliases!);
        Assert.Equal(new[] { "HND", "NRT" }, airports.Where(a => a.City == "Tokyo").Select(a => a.Code).OrderBy(c => c)); // two airports, one city

        var baku = airports.Single(a => a.Code == "GYD");
        Assert.True(baku.IsHub);
        Assert.True(baku.HasRoutes);
        Assert.Single(airports, a => a.IsHub);
        Assert.False(airports.Single(a => a.Code == "NRT").HasRoutes);
    }

    [Fact]
    public async Task TheRouteCheck_SaysWhetherARouteJoinsTwoAirports()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var yes = await (await client.GetAsync("/api/airports/route-check?origin=gyd&destination=LHR")).ReadAsync<RouteCheckDto>();
        var back = await (await client.GetAsync("/api/airports/route-check?origin=LHR&destination=GYD")).ReadAsync<RouteCheckDto>();
        var no = await (await client.GetAsync("/api/airports/route-check?origin=GYD&destination=BCN")).ReadAsync<RouteCheckDto>();

        Assert.Equal(("GYD", "LHR", true), (yes.Origin, yes.Destination, yes.Exists));
        Assert.True(back.Exists);
        Assert.False(no.Exists);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync("/api/airports/route-check?origin=GYD&destination=GYD")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync("/api/airports/route-check?origin=GY&destination=LHR")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/airports/route-check?origin=ZZZ&destination=LHR")).StatusCode);
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
        Assert.Equal(37, first.AirportsAdded); // 39 catalog airports, GYD and LHR were already there
        Assert.Equal(2, first.AirportsLinked);
    }
}
