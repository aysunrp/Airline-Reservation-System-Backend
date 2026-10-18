using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class ExtraServiceOptionsService : IExtraServiceOptionsService
{
    private readonly IBaseRepository<BaggageOption> _baggageRepository;
    private readonly IBaseRepository<MealOption> _mealRepository;

    public ExtraServiceOptionsService(IBaseRepository<BaggageOption> baggageRepository, IBaseRepository<MealOption> mealRepository)
    {
        _baggageRepository = baggageRepository;
        _mealRepository = mealRepository;
    }

    public async Task<IReadOnlyList<BaggageOptionDto>> GetBaggageOptionsAsync(CancellationToken cancellationToken = default)
        => (await _baggageRepository.GetAllAsync(cancellationToken))
            .Where(o => o.IsActive)
            .OrderBy(o => o.Price)
            .ThenBy(o => o.WeightKg)
            .Select(o => o.ToDto())
            .ToList();

    public async Task<IReadOnlyList<MealOptionDto>> GetMealOptionsAsync(CancellationToken cancellationToken = default)
        => (await _mealRepository.GetAllAsync(cancellationToken))
            .Where(o => o.IsActive)
            .OrderBy(o => o.Price)
            .ThenBy(o => o.Name)
            .Select(o => o.ToDto())
            .ToList();
}
