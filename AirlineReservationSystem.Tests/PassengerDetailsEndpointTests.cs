using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Passenger;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Settings;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Title, middle name and the extended gender values travel through the real database and API.</summary>
public class PassengerDetailsEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PassengerDetailsEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Booking_StoresTitleMiddleNameAndGender()
    {
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, "11A", lastName: "Karimova");
        request.Passengers[0].Title = "Mx";
        request.Passengers[0].MiddleName = "Nur";
        request.Passengers[0].Gender = Gender.PreferNotToSay;

        var created = await _factory.CreateClient().PostAsJsonAsync("/api/bookings", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.ReadAsync<BookingDto>();

        var loaded = await (await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}?lastName=Karimova")).ReadAsync<BookingDto>();
        var passenger = Assert.Single(loaded.Passengers);
        Assert.Equal(("Mx", "Nur", Gender.PreferNotToSay), (passenger.Title, passenger.MiddleName, passenger.Gender));

        // Admins see the full name, middle name included.
        var admin = await _factory.CreateAdminClientAsync();
        var row = (await (await admin.GetAsync($"/api/admin/bookings?pnr={booking.Pnr}")).ReadAsync<List<AdminBookingRowDto>>()).Single();
        Assert.Equal("Aysun Nur Karimova", row.LeadPassenger);
        Assert.Single(await (await admin.GetAsync("/api/admin/passengers?search=Aysun Nur")).ReadAsync<List<AdminPassengerDto>>());
    }

    [Fact]
    public async Task Booking_WithoutAValidTitle_IsRejected()
    {
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, "11B");
        request.Passengers[0].Title = "Dr";

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/bookings", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Title must be Mr, Ms, Mrs or Mx.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SavedPassenger_AcceptsTheNewGenderValues()
    {
        await _factory.GetSeedAsync();
        var client = _factory.CreateClient();
        client.WithToken((await client.RegisterAsync()).AccessToken);

        var response = await client.PostAsJsonAsync("/api/profile/saved-passengers", new
        {
            firstName = "Kamal", lastName = "Aliyev", relationship = "Brother", gender = "Other"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"gender\":\"Other\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DocumentRules_FromAppSettings_AreApplied()
    {
        var rules = _factory.Services.GetRequiredService<IOptions<TravelDocumentSettings>>().Value;
        Assert.Equal(new[] { DocumentType.NationalIdCard, DocumentType.Passport }, rules.Domestic["AZ"].ByNationality["AZ"]);
        Assert.Equal(180, rules.International["AE"].MinValidityDays);

        // The seeded flight goes from Baku to London, where an ID card is not accepted.
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, "11C");
        request.Passengers[0].DocumentType = DocumentType.NationalIdCard;
        request.Passengers[0].DocumentNumber = "AA1234567";

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/bookings", request, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("şəxsiyyət vəsiqəsi qəbul edilmir", await response.Content.ReadAsStringAsync());
    }
}
