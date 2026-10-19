using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface ICancellationRuleRepository : IBaseRepository<CancellationRule>
{
    /// <summary>Rules for a cabin, ordered from the largest minimum-days band to the smallest.</summary>
    Task<IReadOnlyList<CancellationRule>> GetByCabinClassAsync(CabinClass cabinClass, CancellationToken cancellationToken = default);

    /// <summary>True when the cabin already has a band starting at this many days.</summary>
    Task<bool> ExistsAsync(CabinClass cabinClass, int minDaysBeforeDeparture, int? excludeId = null, CancellationToken cancellationToken = default);
}
