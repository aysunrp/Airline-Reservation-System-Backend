using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.DTOs.Seat;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class SearchAndOptionsEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SearchAndOptionsEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static object Search(DateTime date, string? cabinClass = null, int passengers = 1, string from = "GYD", string to = "LHR") => new
    {
        legs = new[] { new { originCode = from, destinationCode = to, date = DateOnly.FromDateTime(date) } },
        cabinClass,
        passengers
    };

    [Fact]
    public async Task Airports_AreListed()
    {
        await _factory.GetSeedAsync();

        var airports = await (await _client.GetAsync("/api/airports")).ReadAsync<List<AirportDto>>();

        Assert.Equal(new[] { "Baku", "London" }, airports.Select(a => a.City));
        Assert.Equal(new[] { "Asia/Baku", "Europe/London" }, airports.Select(a => a.TimeZoneId));
    }

    [Fact]
    public async Task Search_ReturnsTheFlightWithFaresAsText()
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _client.PostAsJsonAsync("/api/flights/search", Search(seed.Departure, "Economy", 2), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var flight = json[0].GetProperty("flights")[0];
        Assert.Equal("AV 101", flight.GetProperty("flightNumber").GetString());
        Assert.Equal(420m, flight.GetProperty("fares").GetProperty("Economy").GetDecimal());
        Assert.Equal("Scheduled", flight.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Search_HidesFlightsWithoutTheCabinOrEnoughSeats()
    {
        var seed = await _factory.GetSeedAsync();

        async Task<int> CountAsync(object request)
            => (await (await _client.PostAsJsonAsync("/api/flights/search", request, Json)).ReadAsync<List<FlightSearchLegResultDto>>())[0].Flights.Count;

        Assert.Equal(0, await CountAsync(Search(seed.Departure, "Comfort")));
        Assert.Equal(0, await CountAsync(Search(seed.Departure, "Economy", 4)));
        Assert.Equal(0, await CountAsync(Search(seed.Departure.AddDays(1))));
    }

    [Fact]
    public async Task Search_InvalidOrUnknownInput_Returns400And404()
    {
        var seed = await _factory.GetSeedAsync();

        var sameAirport = await _client.PostAsJsonAsync("/api/flights/search", Search(seed.Departure, from: "GYD", to: "GYD"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, sameAirport.StatusCode);

        var unknownAirport = await _client.PostAsJsonAsync("/api/flights/search", Search(seed.Departure, to: "JFK"), Json);
        Assert.Equal(HttpStatusCode.NotFound, unknownAirport.StatusCode);
    }

    [Fact]
    public async Task FlightDetailsAndSeatMap()
    {
        var seed = await _factory.GetSeedAsync();

        var flight = await (await _client.GetAsync($"/api/flights/{seed.FlightId}")).ReadAsync<FlightResultDto>();
        Assert.Equal("A350-900", flight.AircraftModel);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/flights/999999")).StatusCode);

        var seatMap = await (await _client.GetAsync($"/api/flights/{seed.FlightId}/seats")).ReadAsync<SeatMapDto>();
        Assert.Equal(new[] { "1A", "1B", "11A", "11B", "11C" }, seatMap.Seats.Select(s => s.Label));
        Assert.All(seatMap.Seats, s => Assert.True(s.IsAvailable));

        var business = await (await _client.GetAsync($"/api/flights/{seed.FlightId}/seats?cabinClass=Business")).ReadAsync<SeatMapDto>();
        Assert.Equal(2, business.Seats.Count);
    }

    [Fact]
    public async Task BookingOptions_AreListed()
    {
        await _factory.GetSeedAsync();

        var baggage = await (await _client.GetAsync("/api/booking-options/baggage")).ReadAsync<List<BaggageOptionDto>>();
        var meals = await (await _client.GetAsync("/api/booking-options/meals")).ReadAsync<List<MealOptionDto>>();

        Assert.Equal(new[] { "+10 kg", "+20 kg" }, baggage.Select(o => o.Name));
        Assert.Equal("Classic Dining", Assert.Single(meals).Name);
    }

    [Fact]
    public async Task PassengerRules_ShowTheAgeLimitsAndThePassengerLimit_WithoutSigningIn()
    {
        var response = await _client.GetAsync("/api/booking-options/passenger-rules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PassengerRulesDto(2, 12, 9), await response.ReadAsync<PassengerRulesDto>());
    }

    [Fact]
    public async Task Search_AllowsUpToNineTravellersWhoNeedASeat()
    {
        await _factory.GetSeedAsync();
        var date = DateTime.UtcNow.AddDays(3);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/flights/search", Search(date, passengers: 9))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/flights/search", Search(date, passengers: 10))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/flights/search", Search(date, passengers: 0))).StatusCode);
    }

    [Fact]
    public async Task PromoCodes_ValidReturns200_InvalidReturns422()
    {
        await _factory.GetSeedAsync();

        var valid = await _client.GetAsync("/api/promo-codes/aerova10");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal(10, (await valid.ReadAsync<PromoCodeDto>()).DiscountPercent);

        var expired = await _client.GetAsync("/api/promo-codes/EXPIRED");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        Assert.Equal("Bu promo kod hazırda etibarlı deyil.", (await expired.ReadJsonAsync()).GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.GetAsync("/api/promo-codes/NOPE")).StatusCode);
    }

    [Fact]
    public async Task LowestFares_AreOpenAndReturnOneFarePerDay()
    {
        var seed = await _factory.GetSeedAsync();
        var day = DateOnly.FromDateTime(seed.Departure);
        var url = $"/api/flights/lowest-fares?originCode=GYD&destinationCode=LHR&from={day.AddDays(-2):yyyy-MM-dd}&to={day.AddDays(5):yyyy-MM-dd}";

        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = await response.ReadAsync<List<DayFareDto>>();
        Assert.Equal(day, Assert.Single(days).Date);

        var economy = await (await _client.GetAsync(url + "&cabinClass=Economy")).ReadAsync<List<DayFareDto>>();
        Assert.Single(economy);
        Assert.Empty(await (await _client.GetAsync(url + "&cabinClass=Comfort")).ReadAsync<List<DayFareDto>>());

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/flights/lowest-fares?originCode=GYD&destinationCode=GYD&from=2026-11-01&to=2026-11-05")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/flights/lowest-fares?originCode=GYD&destinationCode=LHR&from=2026-11-01&to=2027-03-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/flights/lowest-fares?originCode=GYD&destinationCode=JFK&from=2026-11-01&to=2026-11-05")).StatusCode);
    }
}
