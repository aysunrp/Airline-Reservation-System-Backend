using Domain.Entities;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class AdminPromoCodeServiceTests
{
    private readonly FakePromoCodeRepository _promoCodes = new(new PromoCode { Id = 1, Code = "AEROVA10", DiscountPercent = 10, MaxUses = 5, UsedCount = 3 });

    private AdminPromoCodeService CreateService() => new(_promoCodes, new PromoCodeRequestValidator());

    [Fact]
    public async Task UsageLimit_CannotGoBelowTheTimesAlreadyUsed()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(1, new PromoCodeRequestDto { Code = "AEROVA10", DiscountPercent = 10, MaxUses = 2 }));

        var updated = await service.UpdateAsync(1, new PromoCodeRequestDto { Code = "AEROVA10", DiscountPercent = 10, MaxUses = 3 });
        Assert.Equal((3, 3), (updated.MaxUses!.Value, updated.UsedCount));
    }

    [Fact]
    public async Task UsedCode_CannotBeDeleted()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().DeleteAsync(1));
        Assert.Single(await _promoCodes.GetAllAsync());
    }

    [Fact]
    public async Task UnzonedDates_AreStoredAsUtc()
    {
        var created = await CreateService().CreateAsync(new PromoCodeRequestDto
        {
            Code = "winter", DiscountPercent = 15, ValidFrom = new DateTime(2026, 12, 1), ValidTo = new DateTime(2027, 2, 28)
        });

        Assert.Equal("WINTER", created.Code);
        Assert.Equal(DateTimeKind.Utc, created.ValidFrom!.Value.Kind);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), created.ValidFrom);
    }
}
