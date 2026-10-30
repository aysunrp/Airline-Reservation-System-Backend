using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The ids of airports that at least one active route starts or ends at.</summary>
    Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>True when an active route joins the two airports (either direction), by IATA code.</summary>
    Task<bool> HasActiveRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when a route, schedule stop or flight segment uses the airport.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
