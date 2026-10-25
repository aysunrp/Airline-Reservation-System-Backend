using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminPromoCodeEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminPromoCodeEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PromoCodes_CrudCycle_AndTheCustomerCheckFollowsTheStatus()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();
        var customer = _factory.CreateClient();

        var created = await admin.PostAsJsonAsync("/api/admin/promo-codes", new PromoCodeRequestDto
        {
            Code = " spring25 ", DiscountPercent = 25, ValidTo = DateTime.UtcNow.AddMonths(1), MaxUses = 100
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var promo = await created.ReadAsync<AdminPromoCodeDto>();
        Assert.Equal(("SPRING25", 25, 100, 0, true), (promo.Code, promo.DiscountPercent, promo.MaxUses, promo.UsedCount, promo.IsActive));
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/promo-codes/spring25")).StatusCode);

        var inactive = new PromoCodeRequestDto { Code = "SPRING25", DiscountPercent = 30, IsActive = false };
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/promo-codes/{promo.Id}", inactive, Json)).ReadAsync<AdminPromoCodeDto>();
        Assert.Equal((30, false, (int?)null), (updated.DiscountPercent, updated.IsActive, updated.MaxUses));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await customer.GetAsync("/api/promo-codes/SPRING25")).StatusCode);
        Assert.Single(await (await admin.GetAsync("/api/admin/promo-codes?search=spring&isActive=false")).ReadAsync<List<AdminPromoCodeDto>>());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/promo-codes/{promo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/promo-codes/{promo.Id}")).StatusCode);
    }

    [Fact]
    public async Task PromoCodes_RulesAreEnforced()
    {
        var seed = await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();

        // The seed already has AEROVA10; codes are compared without case.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/promo-codes",
            new PromoCodeRequestDto { Code = "aerova10", DiscountPercent = 5 }, Json)).StatusCode);

        foreach (var invalid in new[]
                 {
                     new PromoCodeRequestDto { Code = "A!", DiscountPercent = 10 },
                     new PromoCodeRequestDto { Code = "VALID1", DiscountPercent = 0 },
                     new PromoCodeRequestDto { Code = "VALID1", DiscountPercent = 101 },
                     new PromoCodeRequestDto { Code = "VALID1", DiscountPercent = 10, MaxUses = 0 },
                     new PromoCodeRequestDto { Code = "VALID1", DiscountPercent = 10, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow.AddDays(-1) }
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/promo-codes", invalid, Json)).StatusCode);
        }

        // A paid booking that used AEROVA10 keeps the code from being deleted.
        var request = BookingEndpointTests.Request(seed, "11C", lastName: "Quliyeva");
        request.PromoCode = "AEROVA10";
        var customer = _factory.CreateClient();
        var booking = await (await customer.PostAsJsonAsync("/api/bookings", request, Json)).ReadAsync<BookingDto>();
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync("/api/payments", new PaymentRequestDto { Pnr = booking.Pnr, Method = PaymentMethod.ApplePay }, Json)).StatusCode);

        var aerova10 = (await (await admin.GetAsync("/api/admin/promo-codes?search=AEROVA10")).ReadAsync<List<AdminPromoCodeDto>>()).Single();
        Assert.Equal(1, aerova10.UsedCount);
        var inUse = await admin.DeleteAsync($"/api/admin/promo-codes/{aerova10.Id}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inUse.StatusCode);
        Assert.Contains("Set it to inactive", await inUse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/promo-codes/999999")).StatusCode);
    }
}
