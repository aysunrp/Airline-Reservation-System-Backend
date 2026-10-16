using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IFlightSeatRepository : IBaseRepository<FlightSeat>
{
    /// <summary>All seats of a flight with their physical seat info, ordered by row and letter (no tracking).</summary>
    Task<IReadOnlyList<FlightSeat>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken = default);

    /// <summary>Returns tracked seats of one flight (with their physical seat) so their status can be changed in the same unit of work.</summary>
    Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(int flightId, IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default);

    /// <summary>Returns tracked seats by id across flights (used when releasing or confirming a whole booking).</summary>
    Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default);
}
