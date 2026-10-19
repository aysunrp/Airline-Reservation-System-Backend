using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.DTOs.Seat;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Every test uses its own seat of the seeded flight so the tests do not depend on each other.</summary>
public class BookingEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public BookingEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    internal static CreateBookingRequestDto Request(SeedData seed, string? seat, int birthYear = 1990, string lastName = "Panahli") => new()
    {
        TripType = TripType.OneWay,
        // Row 1 is Business, row 11 Economy.
        Legs = { new BookingLegRequestDto { FlightId = seed.FlightId, CabinClass = seat is "1A" or "1B" ? CabinClass.Business : CabinClass.Economy } },
        Passengers =
        {
            new BookingPassengerRequestDto
            {
                Title = "Ms", FirstName = "Aysun", LastName = lastName, DateOfBirth = new DateOnly(birthYear, 3, 1), Gender = Gender.Female,
                NationalityCode = "AZ", DocumentType = DocumentType.Passport, DocumentNumber = "C01234567",
                DocumentIssuingCountry = "AZ", DocumentExpiryDate = new DateOnly(2032, 1, 1),
                BaggageOptionId = seed.BaggageOptionId,
                Seats = seat is null ? new() : new() { new PassengerSeatRequestDto { FlightId = seed.FlightId, FlightSeatId = seed.SeatIds[seat] } }
            }
        },
        ContactEmail = "aysun@example.com",
        ContactPhone = "+994501234567"
    };

    [Fact]
    public async Task Quote_PricesWithoutHoldingAnything()
    {
        var seed = await _factory.GetSeedAsync();
        var request = Request(seed, "1B");
        request.PromoCode = "AEROVA10";

        var response = await _client.PostAsJsonAsync("/api/bookings/quote", request, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var price = await response.ReadAsync<PriceBreakdownDto>();
        // 890 Business fare + 55 baggage + 89 tax = 1034, minus 10% = 930.60
        Assert.Equal(1034m, price.Subtotal);
        Assert.Equal(930.60m, price.Total);

        var seats = await (await _client.GetAsync($"/api/flights/{seed.FlightId}/seats")).ReadAsync<SeatMapDto>();
        Assert.True(seats.Seats.Single(s => s.Label == "1B").IsAvailable);
    }

    [Fact]
    public async Task GuestBooking_Returns201_HoldsTheSeat_AndCanBeManagedByPnr()
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _client.PostAsJsonAsync("/api/bookings", Request(seed, "11A"), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        var pnr = json.GetProperty("pnr").GetString()!;

        var seats = await (await _client.GetAsync($"/api/flights/{seed.FlightId}/seats")).ReadAsync<SeatMapDto>();
        Assert.False(seats.Seats.Single(s => s.Label == "11A").IsAvailable);

        var managed = await _client.GetAsync($"/api/bookings/{pnr.ToLowerInvariant()}?lastName=PANAHLI");
        Assert.Equal(HttpStatusCode.OK, managed.StatusCode);
        Assert.Equal(new[] { "11A" }, (await managed.ReadAsync<BookingDto>()).Legs.Single().Seats);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/bookings/{pnr}?lastName=Aliyev")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/bookings/{pnr}")).StatusCode);
    }

    [Fact]
    public async Task SignedInBooking_ShowsUpInMyTrips()
    {
        var seed = await _factory.GetSeedAsync();
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);

        var created = await (await client.PostAsJsonAsync("/api/bookings", Request(seed, "11B"), Json)).ReadAsync<BookingDto>();

        var trips = await (await client.GetAsync("/api/bookings/my")).ReadAsync<List<BookingSummaryDto>>();
        var trip = Assert.Single(trips);
        Assert.Equal(created.Pnr, trip.Pnr);
        Assert.Equal(1, trip.PassengerCount);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/bookings/my")).StatusCode);
    }

    [Fact]
    public async Task TakenSeat_Returns409()
    {
        var seed = await _factory.GetSeedAsync();
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/bookings", Request(seed, "1A"), Json)).StatusCode);

        var second = await _client.PostAsJsonAsync("/api/bookings", Request(seed, "1A"), Json);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("1A", (await second.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvalidRequests_Return400Or422()
    {
        var seed = await _factory.GetSeedAsync();

        var noExpiry = Request(seed, "11C");
        noExpiry.Passengers[0].DocumentExpiryDate = null;
        var validation = await _client.PostAsJsonAsync("/api/bookings", noExpiry, Json);
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.True((await validation.ReadJsonAsync()).GetProperty("errors").TryGetProperty("Passengers[0].DocumentExpiryDate", out _));

        // A baby travelling alone breaks the "at least one adult" rule.
        var infantAlone = Request(seed, null, birthYear: DateTime.UtcNow.Year);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync("/api/bookings", infantAlone, Json)).StatusCode);
    }

    [Fact]
    public async Task Cancellation_QuoteAndCancel_ReleaseTheSeat()
    {
        var seed = await _factory.GetSeedAsync();
        var booking = await (await _client.PostAsJsonAsync("/api/bookings", Request(seed, "11C", lastName: "Mammadova"), Json)).ReadAsync<BookingDto>();

        var quote = await _client.GetAsync($"/api/bookings/{booking.Pnr}/cancellation-quote?lastName=Mammadova");
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        Assert.Equal(0m, (await quote.ReadAsync<CancellationQuoteDto>()).AmountPaid); // not paid yet

        var wrongName = await _client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Aliyev" }, Json);
        Assert.Equal(HttpStatusCode.NotFound, wrongName.StatusCode);

        var cancelled = await _client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Mammadova", reason = "Plans changed" }, Json);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(BookingStatus.Cancelled, (await cancelled.ReadAsync<BookingDto>()).Status);

        var again = await _client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Mammadova" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var seats = await (await _client.GetAsync($"/api/flights/{seed.FlightId}/seats")).ReadAsync<SeatMapDto>();
        Assert.True(seats.Seats.Single(s => s.Label == "11C").IsAvailable);
    }
}
