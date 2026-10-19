using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when a route, schedule stop or flight segment uses the airport.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
