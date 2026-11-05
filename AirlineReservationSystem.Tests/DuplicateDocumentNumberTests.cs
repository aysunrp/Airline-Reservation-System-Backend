using System.Net;
using System.Net.Http.Json;
using Service.Helpers;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>One document number belongs to one traveller: a booking cannot carry the same number twice.</summary>
public class DuplicateDocumentNumberTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DuplicateDocumentNumberTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>A booking of two adults; the second passenger gets the given document number.</summary>
    private static CreateBookingRequestDto TwoPassengers(SeedData seed, string firstSeat, string secondSeat, string secondNumber, string firstNumber = "C01234567")
    {
        var request = BookingEndpointTests.Request(seed, firstSeat);
        request.Passengers[0].DocumentNumber = firstNumber;

        var second = BookingEndpointTests.Request(seed, secondSeat, birthYear: 1988).Passengers[0];
        second.FirstName = "Leyla";
        second.DocumentNumber = secondNumber;
        request.Passengers.Add(second);
        return request;
    }

    private static async Task AssertDuplicateRefusedAsync(HttpResponseMessage response, int index = 1)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty($"Passengers[{index}].DocumentNumber", out var messages));
        Assert.Contains("1-ci sərnişinin", messages[0].GetString());
    }

    [Theory]
    [InlineData("AB123456", "AB123456")]
    [InlineData("ab123456", "AB123456")]
    [InlineData("AB 123-456", "AB123456")]
    public async Task TheSameNumberOnTwoPassengers_IsRefused_HoweverItIsWritten(string first, string second)
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _client.PostAsJsonAsync("/api/bookings", TwoPassengers(seed, "11A", "11B", second, first), Json);

        await AssertDuplicateRefusedAsync(response);
    }

    [Fact]
    public async Task Quote_AppliesTheSameRule()
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _client.PostAsJsonAsync("/api/bookings/quote", TwoPassengers(seed, "11A", "11B", "c01234567"), Json);

        await AssertDuplicateRefusedAsync(response);
    }

    [Fact]
    public async Task AnInfantWithAnAdultsNumber_IsRefused()
    {
        var seed = await _factory.GetSeedAsync();
        var request = TwoPassengers(seed, "11A", "11B", "C01234567");
        request.Passengers[1].DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
        request.Passengers[1].Seats = new();

        await AssertDuplicateRefusedAsync(await _client.PostAsJsonAsync("/api/bookings", request, Json));
    }

    [Fact]
    public async Task DifferentNumbers_AreAccepted()
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _client.PostAsJsonAsync("/api/bookings", TwoPassengers(seed, "11A", "11B", "D76543210"), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TheSameNumberOnSeparateBookings_IsAccepted()
    {
        var seed = await _factory.GetSeedAsync();

        var first = await _client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A"), Json);
        var second = await _client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11B"), Json);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("ab 123-456", "AB123456")]
    [InlineData(" c0123\t4567 ", "C01234567")]
    [InlineData(null, "")]
    public void Normalize_IgnoresCaseSpacesAndDashes(string? input, string expected)
        => Assert.Equal(expected, DocumentNumbers.Normalize(input));
}
