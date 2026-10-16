using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class RouteRepository : BaseRepository<Route>, IRouteRepository
{
    public RouteRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Route>> GetAllWithAirportsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(r => r.Origin)
            .Include(r => r.Destination)
            .OrderBy(r => r.Origin.Code)
            .ThenBy(r => r.Destination.Code)
            .ToListAsync(cancellationToken);

    public Task<Route?> GetWithAirportsAsync(int id, CancellationToken cancellationToken = default)
        => Set.Include(r => r.Origin)
            .Include(r => r.Destination)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Route?> GetByAirportsAsync(int originAirportId, int destinationAirportId, CancellationToken cancellationToken = default)
        => Set.Include(r => r.Origin)
            .Include(r => r.Destination)
            .FirstOrDefaultAsync(r => r.OriginAirportId == originAirportId && r.DestinationAirportId == destinationAirportId, cancellationToken);

    public Task<bool> ExistsAsync(int originAirportId, int destinationAirportId, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(r => r.OriginAirportId == originAirportId && r.DestinationAirportId == destinationAirportId && r.Id != excludeId, cancellationToken);

    public async Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Set<Flight>().AnyAsync(f => f.RouteId == id, cancellationToken)
           || await Context.Set<FlightSchedule>().AnyAsync(s => s.RouteId == id, cancellationToken);
}
