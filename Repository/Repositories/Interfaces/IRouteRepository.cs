using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IRouteRepository : IBaseRepository<Route>
{
    /// <summary>All routes with their airports, ordered by origin and destination code (no tracking).</summary>
    Task<IReadOnlyList<Route>> GetAllWithAirportsAsync(CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The (origin, destination) airport ids of every active route; the graph connections are found in.</summary>
    Task<IReadOnlyList<(int Origin, int Destination)>> GetActivePairsAsync(CancellationToken cancellationToken = default);

    /// <summary>One route with its airports, tracked so it can be edited.</summary>
    Task<Route?> GetWithAirportsAsync(int id, CancellationToken cancellationToken = default);

    Task<Route?> GetByAirportsAsync(int originAirportId, int destinationAirportId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int originAirportId, int destinationAirportId, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when flights or flight schedules use the route.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
