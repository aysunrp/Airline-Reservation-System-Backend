using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface ISavedPassengerRepository : IBaseRepository<SavedPassenger>
{
    /// <summary>A user's saved passengers ordered by name (no tracking).</summary>
    Task<IReadOnlyList<SavedPassenger>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
