using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class AirportRepository : BaseRepository<Airport>, IAirportRepository
{
    public AirportRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(a => a.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(a => a.CityRecord).ThenInclude(c => c!.Country)
            .Where(a => a.IsActive)
            .OrderBy(a => a.City)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await Context.Set<Route>().AsNoTracking().Where(r => r.IsActive)
            .Select(r => new { r.OriginAirportId, r.DestinationAirportId })
            .ToListAsync(cancellationToken);
        return routes.SelectMany(r => new[] { r.OriginAirportId, r.DestinationAirportId }).ToHashSet();
    }

    public async Task<IReadOnlyList<(string Origin, string Destination)>> GetActiveRoutePairsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Context.Set<Route>().AsNoTracking().Where(r => r.IsActive && r.Origin.IsActive && r.Destination.IsActive)
            .Select(r => new { Origin = r.Origin.Code, Destination = r.Destination.Code })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Origin, r.Destination)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetConnectionAirportCodesAsync(string originCode, string destinationCode, DateTime after, DateTime until, int minWaitMinutes, int maxWaitMinutes, CancellationToken cancellationToken = default)
    {
        var flights = Context.Set<Flight>().AsNoTracking().Where(f => f.Status != Domain.Enums.FlightStatus.Cancelled && f.Route.IsActive);
        var query = from a in flights
                    where a.Route.Origin.Code == originCode && a.Route.Destination.IsActive && a.DepartureAt > after && a.DepartureAt < until
                    join b in flights on a.Route.DestinationAirportId equals b.Route.OriginAirportId
                    where b.Route.Destination.Code == destinationCode && b.Route.DestinationAirportId != a.Route.OriginAirportId
                          && b.DepartureAt >= a.ArrivalAt.AddMinutes(minWaitMinutes) && b.DepartureAt <= a.ArrivalAt.AddMinutes(maxWaitMinutes)
                    select a.Route.Destination.Code;
        return await query.Distinct().OrderBy(code => code).ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default)
        => Context.Set<Route>().AnyAsync(r => r.IsActive
            && ((r.Origin.Code == originCode && r.Destination.Code == destinationCode) || (r.Origin.Code == destinationCode && r.Destination.Code == originCode)), cancellationToken);

    public Task<bool> HasActiveConnectionAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default)
        => Context.Set<Route>().Where(first => first.IsActive && first.Origin.Code == originCode && first.Destination.IsActive)
            .AnyAsync(first => Context.Set<Route>().Any(second => second.IsActive && second.OriginAirportId == first.DestinationAirportId
                                                              && second.Destination.Code == destinationCode
                                                              && second.DestinationAirportId != first.OriginAirportId), cancellationToken);

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(a => a.Code == code && a.Id != excludeId, cancellationToken);

    public async Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Set<Route>().AnyAsync(r => r.OriginAirportId == id || r.DestinationAirportId == id, cancellationToken)
           || await Context.Set<FlightScheduleStop>().AnyAsync(s => s.AirportId == id, cancellationToken)
           || await Context.Set<FlightSegment>().AnyAsync(s => s.OriginAirportId == id || s.DestinationAirportId == id, cancellationToken);
}
