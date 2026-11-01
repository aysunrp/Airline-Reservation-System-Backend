using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class DestinationCatalogRepository : BaseRepository<Country>, IDestinationCatalogRepository
{
    public DestinationCatalogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Country>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(c => c.Cities).ThenInclude(city => city.Airports)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<Country?> GetCountryAsync(int id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<DestinationCity?> GetCityAsync(int id, CancellationToken cancellationToken = default)
        => Context.Set<DestinationCity>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Airport>> GetAirportsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
        => await Context.Set<Airport>().Where(a => codes.Contains(a.Code)).ToListAsync(cancellationToken);

    public async Task AddCityAsync(DestinationCity city, CancellationToken cancellationToken = default)
        => await Context.Set<DestinationCity>().AddAsync(city, cancellationToken);

    public async Task AddAirportAsync(Airport airport, CancellationToken cancellationToken = default)
        => await Context.Set<Airport>().AddAsync(airport, cancellationToken);

    public async Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await Context.Set<Route>().AsNoTracking().Where(r => r.IsActive)
            .Select(r => new { r.OriginAirportId, r.DestinationAirportId })
            .ToListAsync(cancellationToken);
        return routes.SelectMany(r => new[] { r.OriginAirportId, r.DestinationAirportId }).ToHashSet();
    }

    public async Task<(int Routes, int Schedules)> DeactivateRoutesOfAirportsAsync(IReadOnlyCollection<int> airportIds, CancellationToken cancellationToken = default)
    {
        if (airportIds.Count == 0)
        {
            return (0, 0);
        }

        var routes = await Context.Set<Route>()
            .Where(r => r.IsActive && (airportIds.Contains(r.OriginAirportId) || airportIds.Contains(r.DestinationAirportId)))
            .ToListAsync(cancellationToken);
        var routeIds = routes.Select(r => r.Id).ToList();
        var schedules = await Context.Set<FlightSchedule>().Where(s => s.IsActive && routeIds.Contains(s.RouteId)).ToListAsync(cancellationToken);
        routes.ForEach(r => r.IsActive = false);
        schedules.ForEach(s => s.IsActive = false);
        return (routes.Count, schedules.Count);
    }

    public async Task<(int Routes, int Schedules)> DeactivateRoutesBetweenAsync(IReadOnlyCollection<(string Origin, string Destination)> pairs, CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0)
        {
            return (0, 0);
        }

        var wanted = pairs.ToHashSet();
        var active = await Context.Set<Route>().Include(r => r.Origin).Include(r => r.Destination).Where(r => r.IsActive).ToListAsync(cancellationToken);
        var routes = active.Where(r => wanted.Contains((r.Origin.Code, r.Destination.Code))).ToList();
        var routeIds = routes.Select(r => r.Id).ToList();
        var schedules = await Context.Set<FlightSchedule>().Where(s => s.IsActive && routeIds.Contains(s.RouteId)).ToListAsync(cancellationToken);
        routes.ForEach(r => r.IsActive = false);
        schedules.ForEach(s => s.IsActive = false);
        return (routes.Count, schedules.Count);
    }

    public async Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int originAirportId, IReadOnlyCollection<int> destinationAirportIds, DateTime from, int take, CancellationToken cancellationToken = default)
        => await Context.Set<Flight>().AsNoTracking()
            .Include(f => f.Route).ThenInclude(r => r.Origin)
            .Include(f => f.Route).ThenInclude(r => r.Destination)
            .Include(f => f.Fares)
            .Where(f => f.Route.OriginAirportId == originAirportId && f.Route.IsActive
                        && destinationAirportIds.Contains(f.Route.DestinationAirportId)
                        && f.DepartureAt >= from
                        && f.Status != Domain.Enums.FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<DestinationCity?> FindCityAsync(string countryCode, string cityName, CancellationToken cancellationToken = default)
        => Context.Set<DestinationCity>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Country.Code == countryCode && c.Name == cityName, cancellationToken);
}
