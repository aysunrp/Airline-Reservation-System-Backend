using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class FlightScheduleRepository : BaseRepository<FlightSchedule>, IFlightScheduleRepository
{
    public FlightScheduleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<FlightSchedule>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => await WithDetails(Set.AsNoTracking())
            .OrderBy(s => s.FlightNumber)
            .ToListAsync(cancellationToken);

    public Task<FlightSchedule?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        => WithDetails(Set).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    private static IQueryable<FlightSchedule> WithDetails(IQueryable<FlightSchedule> query)
        => query
            .Include(s => s.Route).ThenInclude(r => r.Origin)
            .Include(s => s.Route).ThenInclude(r => r.Destination)
            .Include(s => s.DefaultAircraft);
}
