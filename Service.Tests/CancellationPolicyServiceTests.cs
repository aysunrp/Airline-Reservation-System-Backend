using Domain.Entities;
using Domain.Enums;
using Service.Services;

namespace Service.Tests;

public class CancellationPolicyServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);

    // Mirrors the fare-rule bands used on the booking-details page.
    private static readonly CancellationRule[] Rules =
    {
        new() { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 30, FeePercent = 20 },
        new() { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 14, FeePercent = 40 },
        new() { CabinClass = CabinClass.Comfort, MinDaysBeforeDeparture = 30, FeePercent = 10 },
        new() { CabinClass = CabinClass.Comfort, MinDaysBeforeDeparture = 7, FeePercent = 25 },
        new() { CabinClass = CabinClass.Comfort, MinDaysBeforeDeparture = 1, FeePercent = 50 },
        new() { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 7, FeePercent = 0 },
        new() { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 1, FeePercent = 15 }
    };

    private static readonly CancellationPolicyService Service =
        new(new FakeCancellationRuleRepository(Rules), new FixedTimeProvider(Now));

    [Theory]
    [InlineData(CabinClass.Economy, 45, 20, 100, 400)]
    [InlineData(CabinClass.Economy, 30, 20, 100, 400)]
    [InlineData(CabinClass.Economy, 29, 40, 200, 300)]
    [InlineData(CabinClass.Comfort, 7, 25, 125, 375)]
    [InlineData(CabinClass.Comfort, 1, 50, 250, 250)]
    [InlineData(CabinClass.Business, 10, 0, 0, 500)]
    [InlineData(CabinClass.Business, 3, 15, 75, 425)]
    public async Task RefundableBand_ChargesFeeAndRefundsRest(CabinClass cabin, int daysAhead, decimal feePercent, decimal fee, decimal refund)
    {
        var quote = await Service.GetQuoteAsync(cabin, Now.Date.AddDays(daysAhead).AddHours(9), 500m);

        Assert.True(quote.IsRefundable);
        Assert.Equal(daysAhead, quote.DaysBeforeDeparture);
        Assert.Equal(feePercent, quote.FeePercent);
        Assert.Equal(fee, quote.FeeAmount);
        Assert.Equal(refund, quote.RefundAmount);
    }

    [Theory]
    [InlineData(CabinClass.Economy, 13)]
    [InlineData(CabinClass.Comfort, 0)]
    [InlineData(CabinClass.Business, 0)]
    public async Task OutsideAnyBand_IsNotRefundable(CabinClass cabin, int daysAhead)
    {
        var quote = await Service.GetQuoteAsync(cabin, Now.Date.AddDays(daysAhead).AddHours(9), 500m);

        Assert.False(quote.IsRefundable);
        Assert.Null(quote.FeePercent);
        Assert.Equal(0m, quote.RefundAmount);
        Assert.Equal(500m, quote.FeeAmount);
    }

    [Fact]
    public async Task BandWithoutFeePercent_IsNotRefundable()
    {
        var service = new CancellationPolicyService(
            new FakeCancellationRuleRepository(new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 0, FeePercent = null }),
            new FixedTimeProvider(Now));

        var quote = await service.GetQuoteAsync(CabinClass.Economy, Now.AddDays(5), 300m);

        Assert.False(quote.IsRefundable);
        Assert.Equal(0m, quote.RefundAmount);
    }

    [Fact]
    public async Task Fee_IsRoundedToCents()
    {
        var quote = await Service.GetQuoteAsync(CabinClass.Economy, Now.AddDays(40), 333.33m);

        Assert.Equal(66.67m, quote.FeeAmount);
        Assert.Equal(266.66m, quote.RefundAmount);
    }
}
