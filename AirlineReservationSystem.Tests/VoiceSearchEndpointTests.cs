using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Flight;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Voice flight search over HTTP, as the home page and the results page call it. The Web Speech API of the browser is not involved here
/// (it lives in the page); the assistant behind it is <see cref="TestAssistantProvider"/>, so no live AI request is made.
/// </summary>
public class VoiceSearchEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public VoiceSearchEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Assistant.Reset();
        _client = factory.CreateClient();
    }

    private async Task<VoiceSearchInterpretationDto> InterpretAsync(string text, string? language = null)
    {
        await _factory.GetSeedAsync();
        var response = await _client.PostAsJsonAsync("/api/voice-search/interpret", new { text, language }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<VoiceSearchInterpretationDto>();
    }

    private static DateOnly TomorrowInBaku()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Baku").AddDays(1));

    [Theory]
    [InlineData("Bakından Londona sabah uçuşları göstər.", "az")]
    [InlineData("Show flights from Baku to London tomorrow morning.", "en")]
    [InlineData("Покажи рейсы из Баку в Лондон завтра утром.", "ru")]
    public async Task ASpokenRequest_BecomesTheSearchCriteria_InEveryLanguage(string text, string language)
    {
        var result = await InterpretAsync(text, language);

        Assert.Equal("ready", result.Status);
        Assert.Equal("GYD", result.Origin!.Code);
        Assert.Equal("LHR", result.Destination!.Code);
        Assert.Equal(TomorrowInBaku(), result.Date);
        Assert.Equal(language, result.Language);
        Assert.Equal(1, result.Passengers);
        Assert.False(result.PassengersSpecified);
    }

    [Fact]
    public async Task TheCheapestFlight_IsRecognised_AndGetsAPeriod()
    {
        var result = await InterpretAsync("Find the cheapest flight from Baku to London in the next 30 days.");

        Assert.True(result.Cheapest);
        Assert.Equal(30, result.DateTo!.Value.DayNumber - result.DateFrom!.Value.DayNumber + 1);
    }

    [Fact]
    public async Task NothingSaid_IsNotAnError_ButAnIssueToAskAbout()
    {
        var result = await InterpretAsync("   ");

        Assert.Equal("needs_clarification", result.Status);
        Assert.Equal("missing_text", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task ARequestWithoutALanguage_IsStillRead()
    {
        var response = await _client.PostAsJsonAsync("/api/voice-search/interpret", new { text = "Baku to London" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheAssistantBeingDown_DoesNotStopVoiceSearch()
    {
        _factory.Assistant.Throw = new Service.Helpers.Exceptions.ServiceUnavailableException("down", "assistant_key_rejected");

        var result = await InterpretAsync("Bakından Londona sabah");

        Assert.Equal("ready", result.Status);
        Assert.Equal("rules", result.InterpretedBy);
        _factory.Assistant.Reset();
    }

    [Fact]
    public async Task TheRealFlight_IsFoundByAPeriodSearch_WithItsRealFare()
    {
        var seed = await _factory.GetSeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync("/api/flights/search-range", new
        {
            originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(30), sortBy = "price", passengers = 1
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<FlightRangeSearchResultDto>();
        var flight = result.Flights.Single(f => f.Id == seed.FlightId);
        Assert.Equal(420m, flight.Fares[CabinClass.Economy]);
        Assert.Equal(result.Flights.Count, result.TotalFound);
    }

    [Fact]
    public async Task APeriodSearch_WithNoFlights_IsEmpty_NotAnError()
    {
        await _factory.GetSeedAsync();
        var far = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(50);

        var response = await _client.PostAsJsonAsync("/api/flights/search-range", new { originCode = "GYD", destinationCode = "LHR", from = far, to = far.AddDays(3) }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadAsync<FlightRangeSearchResultDto>()).Flights);
    }

    [Fact]
    public async Task APartOfTheDay_IsAppliedAtTheOriginAirport()
    {
        var seed = await _factory.GetSeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        async Task<int> CountAsync(string part)
        {
            var response = await _client.PostAsJsonAsync("/api/flights/search-range", new
            {
                originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(30), timeOfDay = part
            }, Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.ReadAsync<FlightRangeSearchResultDto>()).Flights.Count(f => f.Id == seed.FlightId);
        }

        // The seeded flight leaves at 09:00 UTC, which is 13:00 in Baku: an afternoon flight.
        Assert.Equal(1, await CountAsync("Afternoon"));
        Assert.Equal(0, await CountAsync("Morning"));
        Assert.Equal(0, await CountAsync("Evening"));
    }

    [Fact]
    public async Task BadPeriodSearches_AreRefused()
    {
        await _factory.GetSeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        async Task<HttpStatusCode> PostAsync(object body) => (await _client.PostAsJsonAsync("/api/flights/search-range", body, Json)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(new { originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(200) }));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(new { originCode = "GYD", destinationCode = "LHR", from = today.AddDays(-5), to = today }));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(new { originCode = "GYD", destinationCode = "GYD", from = today, to = today.AddDays(2) }));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(new { originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(2), passengers = 12 }));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(new { originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(2), sortBy = "random" }));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(new { originCode = "GYD", destinationCode = "XXX", from = today, to = today.AddDays(2) }));
    }

    [Fact]
    public async Task ALimitOfOne_ReturnsOnlyTheCheapestFlight_AndCountsTheOthers()
    {
        var seed = await _factory.GetSeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync("/api/flights/search-range", new
        {
            originCode = "GYD", destinationCode = "LHR", from = today, to = today.AddDays(30), sortBy = "price", limit = 1
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<FlightRangeSearchResultDto>();
        Assert.Equal(seed.FlightId, Assert.Single(result.Flights).Id);
        Assert.Equal(1, result.LowestPriceCount);
    }

    [Fact]
    public async Task ASingularCheapestRequest_IsFlaggedSingle_APluralOneIsNot()
    {
        Assert.True((await InterpretAsync("Bakından Londona ən ucuz uçuş nə vaxtdır?", "az")).CheapestSingle);
        Assert.False((await InterpretAsync("Show me the cheapest flights from Baku to London", "en")).CheapestSingle);
    }

    [Fact]
    public async Task OrdinaryManualSearch_IsUnchanged()
    {
        var seed = await _factory.GetSeedAsync();
        var date = DateOnly.FromDateTime(seed.Departure);

        var response = await _client.PostAsJsonAsync("/api/flights/search", new
        {
            legs = new[] { new { originCode = "GYD", destinationCode = "LHR", date } },
            passengers = 1
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var legs = await response.ReadAsync<List<FlightSearchLegResultDto>>();
        Assert.Equal(seed.FlightId, Assert.Single(Assert.Single(legs).Flights).Id);
    }
}
