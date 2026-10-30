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

    public Task<bool> HasActiveRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default)
        => Context.Set<Route>().AnyAsync(r => r.IsActive
            && ((r.Origin.Code == originCode && r.Destination.Code == destinationCode) || (r.Origin.Code == destinationCode && r.Destination.Code == originCode)), cancellationToken);

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(a => a.Code == code && a.Id != excludeId, cancellationToken);

    public async Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Set<Route>().AnyAsync(r => r.OriginAirportId == id || r.DestinationAirportId == id, cancellationToken)
           || await Context.Set<FlightScheduleStop>().AnyAsync(s => s.AirportId == id, cancellationToken)
           || await Context.Set<FlightSegment>().AnyAsync(s => s.OriginAirportId == id || s.DestinationAirportId == id, cancellationToken);
}
