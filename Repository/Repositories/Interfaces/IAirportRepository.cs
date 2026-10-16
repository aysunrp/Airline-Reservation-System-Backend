using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default);
}
