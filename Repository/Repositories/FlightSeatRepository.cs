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
            .Where(s => s.FlightId == flightId && seatIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
}
