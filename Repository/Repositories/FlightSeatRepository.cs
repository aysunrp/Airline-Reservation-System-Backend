using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class FlightSeatRepository : BaseRepository<FlightSeat>, IFlightSeatRepository
{
    public FlightSeatRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<FlightSeat>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(s => s.AircraftSeat)
            .Where(s => s.FlightId == flightId)
            .OrderBy(s => s.AircraftSeat.RowNumber)
            .ThenBy(s => s.AircraftSeat.Letter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(int flightId, IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default)
        => await Set
            .Include(s => s.AircraftSeat)
            .Where(s => s.FlightId == flightId && seatIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

    public Task<int> CountAvailableOnUpcomingFlightsAsync(DateTime now, CancellationToken cancellationToken = default)
        => Set.CountAsync(s => s.Flight.DepartureAt > now && s.Flight.Status != FlightStatus.Cancelled
                               && (s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil <= now)), cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetFreeSeatCountsAsync(IReadOnlyCollection<int> flightIds, CabinClass? cabinClass, DateTime now, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(s => flightIds.Contains(s.FlightId)
                        && (cabinClass == null || s.AircraftSeat.CabinClass == cabinClass)
                        && (s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil <= now)))
            .GroupBy(s => s.FlightId)
            .Select(g => new { FlightId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FlightId, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default)
        => await Set
            .Where(s => seatIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
}
