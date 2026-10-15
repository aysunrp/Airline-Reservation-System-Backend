using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class BookingRepository : BaseRepository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default)
        => Set.AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets)
            .Include(b => b.Payments)
            .Include(b => b.Cancellation)
            .FirstOrDefaultAsync(b => b.Pnr == pnr, cancellationToken);

    public Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default)
        => Set.AnyAsync(b => b.Pnr == pnr, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(b => b.Flights).ThenInclude(f => f.Flight)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
}
