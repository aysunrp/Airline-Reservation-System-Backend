using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IFlightSeatRepository : IBaseRepository<FlightSeat>
{
    /// <summary>All seats of a flight with their physical seat info, ordered by row and letter (no tracking).</summary>
    Task<IReadOnlyList<FlightSeat>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken = default);

    /// <summary>Returns tracked seats of one flight (with their physical seat) so their status can be changed in the same unit of work.</summary>
    Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(int flightId, IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Free seats per flight (available, or held with an expired hold), optionally for one cabin.
    /// Flights without free seats are missing from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetFreeSeatCountsAsync(IReadOnlyCollection<int> flightIds, CabinClass? cabinClass, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Returns tracked seats by id across flights (used when releasing or confirming a whole booking).</summary>
    Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default);
}
