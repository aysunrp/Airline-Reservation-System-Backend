using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The ids of airports that at least one active route starts or ends at.</summary>
    Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>The active routes between active airports as (origin code, destination code) pairs.</summary>
    Task<IReadOnlyList<(string Origin, string Destination)>> GetActiveRoutePairsAsync(CancellationToken cancellationToken = default);

    /// <summary>True when active routes join the origin to the destination through one transfer airport (origin → hub → destination).</summary>
    Task<bool> HasActiveConnectionAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The codes of the transfer airports through which real flights join the origin to the destination between <paramref name="after"/> and
    /// <paramref name="until"/>: a flight to the transfer airport and a later one from it that leaves <paramref name="minWaitMinutes"/> to
    /// <paramref name="maxWaitMinutes"/> after it lands (cancelled flights ignored).
    /// </summary>
    Task<IReadOnlyList<string>> GetConnectionAirportCodesAsync(string originCode, string destinationCode, DateTime after, DateTime until, int minWaitMinutes, int maxWaitMinutes, CancellationToken cancellationToken = default);

    /// <summary>True when an active route joins the two airports (either direction), by IATA code.</summary>
    Task<bool> HasActiveRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when a route, schedule stop or flight segment uses the airport.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
