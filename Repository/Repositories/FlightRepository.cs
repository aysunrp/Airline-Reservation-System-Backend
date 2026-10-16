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

        return await WithDetails()
            .Where(f => f.Route.OriginAirportId == originAirportId
                        && f.Route.DestinationAirportId == destinationAirportId
                        && f.DepartureAt >= from && f.DepartureAt < to
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => WithDetails().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    // Route airports, aircraft, ordered segments with their airports, and fares: everything a result card shows.
    private IQueryable<Flight> WithDetails()
        => Set.AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Route).ThenInclude(r => r.Origin)
            .Include(f => f.Route).ThenInclude(r => r.Destination)
            .Include(f => f.Aircraft)
            .Include(f => f.Segments.OrderBy(s => s.SequenceNo)).ThenInclude(s => s.Origin)
            .Include(f => f.Segments).ThenInclude(s => s.Destination)
            .Include(f => f.Fares);
}
