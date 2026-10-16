using Domain.Entities;
using Service.Services;

namespace Service.Tests;

public class ExtraServiceOptionsTests
{
    private readonly ExtraServiceOptionsService _service = new(
        new FakeRepository<BaggageOption>(TestData.BaggageOptions),
        new FakeRepository<MealOption>(TestData.MealOptions));

    [Fact]
    public async Task BaggageOptions_OnlyActive_CheapestFirst()
    {
        var options = await _service.GetBaggageOptionsAsync();

        Assert.Equal(new[] { "+10 kg", "+20 kg" }, options.Select(o => o.Name));
        Assert.Equal(new[] { 10, 20 }, options.Select(o => o.WeightKg));
        Assert.Equal(new[] { 30m, 50m }, options.Select(o => o.Price));
    }

    [Fact]
    public async Task MealOptions_AreListedWithPrices()
    {
        var options = await _service.GetMealOptionsAsync();

        Assert.Equal(new[] { "Classic Dining", "Signature Dining" }, options.Select(o => o.Name));
        Assert.Equal(new[] { 18m, 35m }, options.Select(o => o.Price));
    }
}
