using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>Choices shown on the baggage and meal selection pages.</summary>
public interface IExtraServiceOptionsService
{
    /// <summary>Active baggage options, cheapest first.</summary>
    Task<IReadOnlyList<BaggageOptionDto>> GetBaggageOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active meal options, cheapest first.</summary>
    Task<IReadOnlyList<MealOptionDto>> GetMealOptionsAsync(CancellationToken cancellationToken = default);
}
