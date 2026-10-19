using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminBookingOptionEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminBookingOptionEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BaggageOptions_CrudCycle_AndOnlyActiveOnesAreOffered()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var customer = _factory.CreateClient();

        var created = await admin.PostAsJsonAsync("/api/admin/booking-options/baggage",
            new BaggageOptionRequestDto { Code = " BAG30 ", Name = "+30 kg", WeightKg = 30, Price = 75 }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var option = await created.ReadAsync<BaggageOptionDto>();
        Assert.Equal(("bag30", 30, 75m, true), (option.Code, option.WeightKg, option.Price, option.IsActive));
        Assert.Contains(await (await customer.GetAsync("/api/booking-options/baggage")).ReadAsync<List<BaggageOptionDto>>(), o => o.Code == "bag30");

        var updated = await (await admin.PutAsJsonAsync($"/api/admin/booking-options/baggage/{option.Id}",
            new BaggageOptionRequestDto { Code = "bag30", Name = "+30 kg", WeightKg = 30, Price = 80, IsActive = false }, Json)).ReadAsync<BaggageOptionDto>();
        Assert.Equal((80m, false), (updated.Price, updated.IsActive));
        Assert.DoesNotContain(await (await customer.GetAsync("/api/booking-options/baggage")).ReadAsync<List<BaggageOptionDto>>(), o => o.Code == "bag30");
        Assert.Contains(await (await admin.GetAsync("/api/admin/booking-options/baggage")).ReadAsync<List<BaggageOptionDto>>(), o => o.Code == "bag30");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/booking-options/baggage/{option.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/booking-options/baggage/{option.Id}")).StatusCode);
    }

    [Fact]
    public async Task MealOptions_CrudCycle()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/booking-options/meals",
            new MealOptionRequestDto { Code = "wellness", Name = "Wellness Dining", Description = "  ", Price = 25 }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var option = await created.ReadAsync<MealOptionDto>();
        Assert.Null(option.Description);

        var updated = await (await admin.PutAsJsonAsync($"/api/admin/booking-options/meals/{option.Id}",
            new MealOptionRequestDto { Code = "wellness", Name = "Wellness Dining", Description = "Fresh and balanced.", Price = 27 }, Json)).ReadAsync<MealOptionDto>();
        Assert.Equal(("Fresh and balanced.", 27m), (updated.Description, updated.Price));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/booking-options/meals/{option.Id}")).StatusCode);
    }

    [Fact]
    public async Task Options_RulesAreEnforced()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateAdminClientAsync();

        // The seed has bag20 and classic; codes are compared without case.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/booking-options/baggage",
            new BaggageOptionRequestDto { Code = "BAG20", Name = "+20 kg", WeightKg = 20, Price = 55 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/booking-options/meals",
            new MealOptionRequestDto { Code = "Classic", Name = "Classic", Price = 18 }, Json)).StatusCode);

        foreach (var invalid in new[]
                 {
                     new BaggageOptionRequestDto { Code = "x", Name = "+5 kg", WeightKg = 5, Price = 10 },
                     new BaggageOptionRequestDto { Code = "bag0", Name = "+0 kg", WeightKg = 0, Price = 10 },
                     new BaggageOptionRequestDto { Code = "bag5", Name = "+5 kg", WeightKg = 5, Price = -1 },
                     new BaggageOptionRequestDto { Code = "bag5", Name = "", WeightKg = 5, Price = 10 }
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/booking-options/baggage", invalid, Json)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/booking-options/meals",
            new MealOptionRequestDto { Code = "vegan", Name = "Vegan", Description = new string('x', 501), Price = 20 }, Json)).StatusCode);

        // Options sold with a ticket stay: this booking takes the seeded baggage and meal.
        var request = BookingEndpointTests.Request(seed, "11B", lastName: "Hasanova");
        request.Passengers[0].MealOptionId = seed.MealOptionId;
        Assert.Equal(HttpStatusCode.Created, (await _factory.CreateClient().PostAsJsonAsync("/api/bookings", request, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/admin/booking-options/baggage/{seed.BaggageOptionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/admin/booking-options/meals/{seed.MealOptionId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/booking-options/meals/999999")).StatusCode);
    }
}
