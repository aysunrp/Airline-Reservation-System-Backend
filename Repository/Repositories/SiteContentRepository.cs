using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class SiteContentRepository : BaseRepository<SiteContent>, ISiteContentRepository
{
    public SiteContentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<SiteContent>> GetByPageAsync(string page, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(c => c.Page == page).OrderBy(c => c.Key).ToListAsync(cancellationToken);

    public async Task<HashSet<string>> GetKeysAsync(string page, CancellationToken cancellationToken = default)
        => (await Set.AsNoTracking().Where(c => c.Page == page).Select(c => c.Key).ToListAsync(cancellationToken)).ToHashSet();

    public Task<SiteContent?> FindTrackedAsync(string page, string key, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(c => c.Page == page && c.Key == key, cancellationToken);
}
