using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminCancellationRuleEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminCancellationRuleEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rules_CrudCycle_AndQuotesUseTheNewValues()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/cancellation-rules",
            new CancellationRuleRequestDto { CabinClass = CabinClass.Comfort, MinDaysBeforeDeparture = 30, FeePercent = 10 }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rule = await created.ReadAsync<CancellationRuleDto>();
        Assert.Equal((CabinClass.Comfort, 30, (decimal?)10), (rule.CabinClass, rule.MinDaysBeforeDeparture, rule.FeePercent));
        Assert.Single(await (await admin.GetAsync("/api/admin/cancellation-rules?cabinClass=Comfort")).ReadAsync<List<CancellationRuleDto>>());

        var noRefund = await (await admin.PutAsJsonAsync($"/api/admin/cancellation-rules/{rule.Id}",
            new CancellationRuleRequestDto { CabinClass = CabinClass.Comfort, MinDaysBeforeDeparture = 30, FeePercent = null }, Json)).ReadAsync<CancellationRuleDto>();
        Assert.Null(noRefund.FeePercent);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/cancellation-rules/{rule.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/cancellation-rules/{rule.Id}")).StatusCode);

        // The seeded Economy band (7+ days: 20%) now costs 50%; an Economy booking 10 days out is quoted with it.
        var economy = (await (await admin.GetAsync("/api/admin/cancellation-rules?cabinClass=Economy")).ReadAsync<List<CancellationRuleDto>>()).Single();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/cancellation-rules/{economy.Id}",
            new CancellationRuleRequestDto { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 7, FeePercent = 50 }, Json)).StatusCode);

        var booking = await (await _factory.CreateClient().PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A", lastName: "Rzayeva"), Json)).ReadAsync<BookingDto>();
        var quote = await (await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}/cancellation-quote?lastName=Rzayeva")).ReadAsync<CancellationQuoteDto>();
        Assert.Equal(50m, quote.FeePercent);
    }

    [Fact]
    public async Task Rules_RulesAreEnforced()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();

        // The seed already has Business from 1 day.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/cancellation-rules",
            new CancellationRuleRequestDto { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 1, FeePercent = 15 }, Json)).StatusCode);

        foreach (var invalid in new[]
                 {
                     new CancellationRuleRequestDto { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = -1, FeePercent = 10 },
                     new CancellationRuleRequestDto { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 400, FeePercent = 10 },
                     new CancellationRuleRequestDto { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 3, FeePercent = 101 },
                     new CancellationRuleRequestDto { CabinClass = (CabinClass)9, MinDaysBeforeDeparture = 3, FeePercent = 10 }
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/cancellation-rules", invalid, Json)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/cancellation-rules/999999",
            new CancellationRuleRequestDto { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 3, FeePercent = 10 }, Json)).StatusCode);
    }
}
