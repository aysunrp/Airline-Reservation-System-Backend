using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class HomeCardRepository : BaseRepository<HomeCard>, IHomeCardRepository
{
    public HomeCardRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<HomeCard>> GetOrderedAsync(string? section, bool activeOnly, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(x => (section == null || x.Section == section) && (!activeOnly || x.IsActive))
            .OrderBy(x => x.Section).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> NextSortOrderAsync(string section, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(x => x.Section == section, cancellationToken)
            ? await Set.Where(x => x.Section == section).MaxAsync(x => x.SortOrder, cancellationToken) + 1
            : 1;

    public Task<bool> ImagePathUsedAsync(string imagePath, int? excludeId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(x => x.ImagePath == imagePath && x.Id != excludeId, cancellationToken);
}
