using Domain.Entities;
using Domain.Enums;
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
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.FlightSeat!).ThenInclude(s => s.AircraftSeat)
            .Include(b => b.Payments)
            .Include(b => b.Cancellation!).ThenInclude(c => c.Refunds)
            .Include(b => b.PromoCode)
            .FirstOrDefaultAsync(b => b.Pnr == pnr, cancellationToken);

    public Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default)
        => Set.AnyAsync(b => b.Pnr == pnr, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetForAdminAsync(string? pnr, string? passengerName, BookingStatus? status, int? take = null, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.FlightSeat!).ThenInclude(s => s.AircraftSeat)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pnr))
        {
            var fragment = pnr.Trim();
            query = query.Where(b => b.Pnr.Contains(fragment));
        }

        if (!string.IsNullOrWhiteSpace(passengerName))
        {
            var fragment = passengerName.Trim();
            query = query.Where(b => b.Passengers.Any(p => (p.FirstName + " " + p.LastName).Contains(fragment)));
        }

        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        query = query.OrderByDescending(b => b.CreatedAt);
        if (take is not null)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetUserIdsWithActiveBookingsOnFlightAsync(int flightId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(b => b.UserId != null
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.Flights.Any(f => f.FlightId == flightId))
            .Select(b => b.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets)
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt != null && b.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
}
