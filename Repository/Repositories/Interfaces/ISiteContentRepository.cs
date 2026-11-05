using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface ISiteContentRepository : IBaseRepository<SiteContent>
{
    /// <summary>All texts of one page without change tracking, ordered by key.</summary>
    Task<IReadOnlyList<SiteContent>> GetByPageAsync(string page, CancellationToken cancellationToken = default);

    /// <summary>The keys that already exist on a page (used by the seeder to add only the missing ones).</summary>
    Task<HashSet<string>> GetKeysAsync(string page, CancellationToken cancellationToken = default);

    Task<SiteContent?> FindTrackedAsync(string page, string key, CancellationToken cancellationToken = default);

    /// <summary>Whether any sight-media row stores this relative path (optional key excluded while replacing one row).</summary>
    Task<bool> IsSightMediaPathUsedAsync(string relativePath, string? exceptKey = null, CancellationToken cancellationToken = default);
}
