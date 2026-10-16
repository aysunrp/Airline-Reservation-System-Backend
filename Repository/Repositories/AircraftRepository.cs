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

    public async Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Set<Flight>().AnyAsync(f => f.AircraftId == id, cancellationToken)
           || await Context.Set<FlightSchedule>().AnyAsync(s => s.DefaultAircraftId == id, cancellationToken);
}
