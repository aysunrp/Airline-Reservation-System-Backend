using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

public class PricingServiceTests
{
    private static readonly DateOnly TravelDate = new(2026, 11, 10);
    private readonly PricingService _service = new(Options.Create(new PricingSettings()));

    [Theory]
    [InlineData("2025-11-11", PassengerType.Infant)] // one day short of 1 year
    [InlineData("2024-11-11", PassengerType.Infant)] // one day short of 2 years
    [InlineData("2024-11-10", PassengerType.Child)]  // turns 2 on the travel date
    [InlineData("2014-11-11", PassengerType.Child)]  // one day short of 12 years
    [InlineData("2014-11-10", PassengerType.Adult)]  // turns 12 on the travel date
    [InlineData("1990-05-01", PassengerType.Adult)]
    public void GetPassengerType_UsesAgeOnTravelDate(string dateOfBirth, PassengerType expected)
    {
        Assert.Equal(expected, _service.GetPassengerType(DateOnly.Parse(dateOfBirth), TravelDate));
    }

    [Fact]
    public void GetPassengerType_BirthAfterTravelDate_Throws()
    {
        Assert.Throws<BusinessRuleException>(() => _service.GetPassengerType(TravelDate.AddDays(1), TravelDate));
    }

    [Theory]
    [InlineData(PassengerType.Adult, 420)]
    [InlineData(PassengerType.Child, 420)]
    [InlineData(PassengerType.Infant, 42)]
    public void GetPassengerFare_AppliesFareRate(PassengerType type, decimal expected)
    {
        Assert.Equal(expected, _service.GetPassengerFare(420m, type));
    }

    [Fact]
    public void Calculate_WithoutPromo_MatchesPaymentPageFormula()
    {
        var result = _service.Calculate(baseFare: 420m, baggage: 30m, meals: 18m, promoCode: null);

        Assert.Equal(42m, result.Taxes);
        Assert.Equal(510m, result.Subtotal);
        Assert.Equal(0m, result.Discount);
        Assert.Equal(510m, result.Total);
        Assert.Null(result.PromoCode);
    }

    [Fact]
    public void Calculate_WithPromo_DiscountsTheSubtotal()
    {
        var result = _service.Calculate(420m, 30m, 18m, new PromoCodeDto(1, "AEROVA10", 10));

        Assert.Equal(51m, result.Discount);
        Assert.Equal(459m, result.Total);
        Assert.Equal("AEROVA10", result.PromoCode);
        Assert.Equal(10, result.PromoPercent);
    }

    [Fact]
    public void Calculate_RoundsToCents()
    {
        var result = _service.Calculate(333.33m, 0m, 0m, new PromoCodeDto(1, "FLYAEROVA", 15));

        Assert.Equal(33.33m, result.Taxes);
        Assert.Equal(366.66m, result.Subtotal);
        Assert.Equal(55m, result.Discount);
        Assert.Equal(311.66m, result.Total);
    }

    [Fact]
    public void Settings_AreConfigurable()
    {
        var service = new PricingService(Options.Create(new PricingSettings { InfantFareRate = 0m, TaxRate = 0.2m }));

        Assert.Equal(0m, service.GetPassengerFare(420m, PassengerType.Infant));
        Assert.Equal(20m, service.Calculate(100m, 0m, 0m, null).Taxes);
    }
}
