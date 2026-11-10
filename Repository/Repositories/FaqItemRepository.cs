using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class FaqItemRepository : BaseRepository<FaqItem>, IFaqItemRepository
{
    public FaqItemRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<FaqItem>> GetOrderedAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> NextSortOrderAsync(CancellationToken cancellationToken = default)
        => await Set.AnyAsync(cancellationToken) ? await Set.MaxAsync(x => x.SortOrder, cancellationToken) + 1 : 1;
}
