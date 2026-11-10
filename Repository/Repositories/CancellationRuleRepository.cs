using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class CancellationRuleRepository : BaseRepository<CancellationRule>, ICancellationRuleRepository
{
    public CancellationRuleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CancellationRule>> GetByCabinClassAsync(CabinClass cabinClass, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(r => r.CabinClass == cabinClass)
            .OrderByDescending(r => r.MinDaysBeforeDeparture)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(CabinClass cabinClass, int minDaysBeforeDeparture, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(r => r.CabinClass == cabinClass && r.MinDaysBeforeDeparture == minDaysBeforeDeparture && r.Id != excludeId, cancellationToken);
}
