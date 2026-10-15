using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IFlightRepository : IBaseRepository<Flight>
{
    /// <summary>Non-cancelled flights on the given route and UTC day, with fares and segments, ordered by departure.</summary>
    Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Returns a flight with route, aircraft, segments and fares (no tracking).</summary>
    Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);
}
