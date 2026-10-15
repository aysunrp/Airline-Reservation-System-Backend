using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class FlightRepository : BaseRepository<Flight>, IFlightRepository
{
    public FlightRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        return await Set.AsNoTracking()
            .Include(f => f.Fares)
            .Include(f => f.Segments.OrderBy(s => s.SequenceNo))
            .Where(f => f.Route.OriginAirportId == originAirportId
                        && f.Route.DestinationAirportId == destinationAirportId
                        && f.DepartureAt >= from && f.DepartureAt < to
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Include(f => f.Route)
            .Include(f => f.Aircraft)
            .Include(f => f.Segments.OrderBy(s => s.SequenceNo))
            .Include(f => f.Fares)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
}
