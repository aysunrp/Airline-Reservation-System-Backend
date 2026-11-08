using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class AircraftRepository : BaseRepository<Aircraft>, IAircraftRepository
{
    public AircraftRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Aircraft>> GetAllWithSeatsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(a => a.Seats)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public Task<Aircraft?> GetWithSeatsAsync(int id, CancellationToken cancellationToken = default)
        => Set.Include(a => a.Seats).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> RegistrationExistsAsync(string registrationNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(a => a.RegistrationNumber == registrationNumber && a.Id != excludeId, cancellationToken);

    public async Task<IReadOnlyList<AircraftOverlapRow>> GetOverlapsAsync(DateTime after, CancellationToken cancellationToken = default)
    {
        var flights = Context.Set<Flight>().AsNoTracking().Where(f => f.Status != Domain.Enums.FlightStatus.Cancelled);
        var pairs = from a in flights
                    join b in flights on a.AircraftId equals b.AircraftId
                    where a.Id < b.Id && a.DepartureAt > after && a.DepartureAt < b.ArrivalAt && b.DepartureAt < a.ArrivalAt
                    select new { a.AircraftId, FlightA = a.FlightNumber, FlightB = b.FlightNumber, a.DepartureAt };

        var counts = await pairs.GroupBy(p => p.AircraftId).Select(g => new { AircraftId = g.Key, Pairs = g.Count() }).ToListAsync(cancellationToken);
        var rows = new List<AircraftOverlapRow>();
        foreach (var count in counts.OrderByDescending(c => c.Pairs))
        {
            var first = await pairs.Where(p => p.AircraftId == count.AircraftId).OrderBy(p => p.DepartureAt).FirstAsync(cancellationToken);
            rows.Add(new AircraftOverlapRow(count.AircraftId, count.Pairs, first.FlightA, first.FlightB, first.DepartureAt));
        }

        return rows;
    }

    public async Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Set<Flight>().AnyAsync(f => f.AircraftId == id, cancellationToken)
           || await Context.Set<FlightSchedule>().AnyAsync(s => s.DefaultAircraftId == id, cancellationToken);
}
