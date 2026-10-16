using Domain.Entities;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

public class PromoCodeServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PromoCodeService CreateService(params PromoCode[] codes)
        => new(new FakePromoCodeRepository(codes), new FixedTimeProvider(Now));

    [Fact]
    public async Task ValidCode_IsReturned_CaseAndSpacesIgnored()
    {
        var service = CreateService(new PromoCode { Id = 7, Code = "AEROVA10", DiscountPercent = 10 });

        var result = await service.ValidateAsync("  aerova10 ");

        Assert.Equal(7, result.Id);
        Assert.Equal("AEROVA10", result.Code);
        Assert.Equal(10, result.DiscountPercent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UNKNOWN")]
    public async Task EmptyOrUnknownCode_Throws(string code)
    {
        var service = CreateService(new PromoCode { Code = "AEROVA10", DiscountPercent = 10 });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync(code));
    }

    [Fact]
    public async Task InactiveCode_Throws()
    {
        var service = CreateService(new PromoCode { Code = "WELCOME20", DiscountPercent = 20, IsActive = false });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync("WELCOME20"));
    }

    [Fact]
    public async Task ExpiredOrNotStartedCode_Throws()
    {
        var service = CreateService(
            new PromoCode { Code = "OLD", DiscountPercent = 10, ValidTo = Now.AddDays(-1) },
            new PromoCode { Code = "SOON", DiscountPercent = 10, ValidFrom = Now.AddDays(1) });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync("OLD"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync("SOON"));
    }

    [Fact]
    public async Task CodeWithinValidityWindow_IsAccepted()
    {
        var service = CreateService(new PromoCode { Code = "NOW", DiscountPercent = 15, ValidFrom = Now.AddDays(-1), ValidTo = Now.AddDays(1) });

        Assert.Equal(15, (await service.ValidateAsync("NOW")).DiscountPercent);
    }

    [Fact]
    public async Task UsageLimitReached_Throws()
    {
        var service = CreateService(
            new PromoCode { Code = "FULL", DiscountPercent = 10, MaxUses = 3, UsedCount = 3 },
            new PromoCode { Code = "LEFT", DiscountPercent = 10, MaxUses = 3, UsedCount = 2 });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync("FULL"));
        Assert.Equal("LEFT", (await service.ValidateAsync("LEFT")).Code);
    }
}
