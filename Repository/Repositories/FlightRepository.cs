using Domain.Entities;
using Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class FlightRepository : BaseRepository<Flight>, IFlightRepository
{
    public FlightRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => await WithDetails()
            .Where(f => f.Route.OriginAirportId == originAirportId
                        && f.Route.DestinationAirportId == destinationAirportId
                        && f.DepartureAt >= fromUtc && f.DepartureAt < toUtc
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToListAsync(cancellationToken);

    public Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => WithDetails().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => await WithDetails()
            .OrderBy(f => f.DepartureAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetUpcomingAsync(DateTime from, int take, CancellationToken cancellationToken = default)
        => await WithDetails()
            .Where(f => f.DepartureAt >= from && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Flight?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default)
        => Set.AsSplitQuery()
            .Include(f => f.Route).ThenInclude(r => r.Origin)
            .Include(f => f.Route).ThenInclude(r => r.Destination)
            .Include(f => f.Aircraft)
            .Include(f => f.Segments).ThenInclude(s => s.Origin)
            .Include(f => f.Segments).ThenInclude(s => s.Destination)
            .Include(f => f.Fares)
            .Include(f => f.Seats)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string flightNumber, DateTime departureAt, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(f => f.FlightNumber == flightNumber && f.DepartureAt == departureAt && f.Id != excludeId, cancellationToken);

    public async Task<IReadOnlyList<(string FlightNumber, DateTime DepartureAt)>> GetDepartureKeysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => (await Set.AsNoTracking()
                .Where(f => f.DepartureAt >= fromUtc && f.DepartureAt < toUtc)
                .Select(f => new { f.FlightNumber, f.DepartureAt })
                .ToListAsync(cancellationToken))
            .Select(f => (f.FlightNumber, f.DepartureAt))
            .ToList();

    public async Task<IReadOnlyList<DateOnly>> GetScheduledDatesAsync(int scheduleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(f => f.FlightScheduleId == scheduleId && f.ScheduledDate >= from && f.ScheduledDate <= to)
            .Select(f => f.ScheduledDate!.Value)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(flight, cancellationToken);
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Duplicate key: the whole insert was rolled back, so drop the rejected flight and its children too.
            foreach (var entry in Context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public void ClearTracking() => Context.ChangeTracker.Clear();

    public async Task<IReadOnlyList<Flight>> GetLandedOpenAsync(DateTime now, CancellationToken cancellationToken = default)
        => await Set
            .Where(f => f.ArrivalAt <= now && f.Status != FlightStatus.Cancelled && f.Status != FlightStatus.Completed)
            .ToListAsync(cancellationToken);

    public Task<bool> HasBookingsAsync(int id, CancellationToken cancellationToken = default)
        => Context.Set<BookingFlight>().AnyAsync(bf => bf.FlightId == id, cancellationToken);

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
