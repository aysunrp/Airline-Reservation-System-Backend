using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IFaqItemRepository : IBaseRepository<FaqItem>
{
    /// <summary>The items in their display order (without tracking); only the active ones for the public page.</summary>
    Task<IReadOnlyList<FaqItem>> GetOrderedAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<int> NextSortOrderAsync(CancellationToken cancellationToken = default);
}
