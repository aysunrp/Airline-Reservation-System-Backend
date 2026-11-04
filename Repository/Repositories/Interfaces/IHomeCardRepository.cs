using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IHomeCardRepository : IBaseRepository<HomeCard>
{
    /// <summary>The cards of a section (every section when null) in their display order, without tracking; only the active ones for the public page.</summary>
    Task<IReadOnlyList<HomeCard>> GetOrderedAsync(string? section, bool activeOnly, CancellationToken cancellationToken = default);

    Task<int> NextSortOrderAsync(string section, CancellationToken cancellationToken = default);

    /// <summary>True when another card uses the same image path (a file shared by two cards is not deleted with one of them).</summary>
    Task<bool> ImagePathUsedAsync(string imagePath, int? excludeId, CancellationToken cancellationToken = default);
}
