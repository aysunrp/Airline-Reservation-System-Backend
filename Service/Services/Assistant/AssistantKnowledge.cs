using System.Globalization;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Reads the fare rules, baggage options and meal options (the settings AEROVA prices and cancels with) and writes them as text. The
/// text is kept for a few minutes so a chat does not read the database for every question. A failed read leaves the assistant without
/// these facts (it then says it cannot verify them) and never fails the chat.
/// </summary>
/// <summary>The facts text and how long it is good for; one instance for the whole application, so the repositories (which live per request) are not kept.</summary>
public class AssistantFactsCache
{
    public string Text { get; set; } = string.Empty;

    public DateTime Until { get; set; } = DateTime.MinValue;
}

public class AssistantKnowledge : IAssistantKnowledge
{
    private readonly ICancellationRuleRepository _rules;
    private readonly IBaseRepository<BaggageOption> _baggage;
    private readonly IBaseRepository<MealOption> _meals;
    private readonly TravelAssistantSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssistantKnowledge> _logger;

    private readonly AssistantFactsCache _cache;

    public AssistantKnowledge(
        ICancellationRuleRepository rules,
        IBaseRepository<BaggageOption> baggage,
        IBaseRepository<MealOption> meals,
        IOptions<TravelAssistantSettings> settings,
        AssistantFactsCache cache,
        TimeProvider timeProvider,
        ILogger<AssistantKnowledge> logger)
    {
        _cache = cache;
        _rules = rules;
        _baggage = baggage;
        _meals = meals;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> GetFactsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now < _cache.Until)
        {
            return _cache.Text;
        }

        try
        {
            var facts = Describe(await _rules.GetAllAsync(cancellationToken), await _baggage.GetAllAsync(cancellationToken), await _meals.GetAllAsync(cancellationToken));
            _cache.Text = facts;
            _cache.Until = now.AddMinutes(Math.Max(1, _settings.FactsCacheMinutes));
            return facts;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The facts for the assistant could not be read; it answers without them.");
            return string.Empty;
        }
    }

    /// <summary>The configuration as text. Prices are in USD, the currency the booking pages show.</summary>
    public static string Describe(IEnumerable<CancellationRule> rules, IEnumerable<BaggageOption> baggage, IEnumerable<MealOption> meals)
    {
        var text = new StringBuilder();

        var byCabin = rules.GroupBy(r => r.CabinClass).OrderBy(g => g.Key).ToList();
        if (byCabin.Count > 0)
        {
            text.AppendLine("Cancellation and refund rules (fee kept from the amount paid; the band with the largest number of days that has been reached applies):");
            foreach (var cabin in byCabin)
            {
                var bands = cabin.OrderByDescending(r => r.MinDaysBeforeDeparture)
                    .Select(r => r.FeePercent is decimal fee
                        ? $"{r.MinDaysBeforeDeparture}+ days before departure: {fee.ToString("0.##", CultureInfo.InvariantCulture)}% fee"
                        : $"{r.MinDaysBeforeDeparture}+ days before departure: cancellable, no refund");
                text.AppendLine($"- {cabin.Key}: {string.Join("; ", bands)}; closer to departure than the last band: cancellable, no refund.");
            }
        }

        var bags = baggage.Where(b => b.IsActive).OrderBy(b => b.WeightKg).ToList();
        if (bags.Count > 0)
        {
            text.AppendLine("Extra baggage options sold at booking: " + string.Join("; ", bags.Select(b =>
                $"{b.Name} ({b.WeightKg} kg, {b.Price.ToString("0.00", CultureInfo.InvariantCulture)} USD)")) + ".");
        }

        var meal = meals.Where(m => m.IsActive).OrderBy(m => m.Price).ToList();
        if (meal.Count > 0)
        {
            text.AppendLine("Meal options sold at booking: " + string.Join("; ", meal.Select(m =>
                $"{m.Name} ({m.Price.ToString("0.00", CultureInfo.InvariantCulture)} USD)")) + ".");
        }

        return text.ToString().Trim();
    }
}
