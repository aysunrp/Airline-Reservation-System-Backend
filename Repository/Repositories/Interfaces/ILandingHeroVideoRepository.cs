using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface ILandingHeroVideoRepository : IBaseRepository<LandingHeroVideo>
{
    Task<IReadOnlyList<LandingHeroVideo>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    Task<LandingHeroVideo?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandingHeroVideo>> GetAllActiveTrackedAsync(CancellationToken cancellationToken = default);
}
