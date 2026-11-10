using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class LandingHeroVideoRepository : BaseRepository<LandingHeroVideo>, ILandingHeroVideoRepository
{
    public LandingHeroVideoRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<LandingHeroVideo>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .OrderByDescending(v => v.IsActive)
            .ThenBy(v => v.SortOrder)
            .ThenByDescending(v => v.Id)
            .ToListAsync(cancellationToken);

    public async Task<LandingHeroVideo?> GetActiveAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.SortOrder).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LandingHeroVideo>> GetAllActiveTrackedAsync(CancellationToken cancellationToken = default)
        => await Set.Where(v => v.IsActive).ToListAsync(cancellationToken);
}
