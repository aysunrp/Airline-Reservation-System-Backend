using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class AccountErasureRepository : IAccountErasureRepository
{
    private readonly AppDbContext _context;

    public AccountErasureRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasOpenTripsAsync(int userId, DateTime now, CancellationToken cancellationToken = default)
    {
        var heldOrFlying = await _context.Bookings.AnyAsync(b => b.UserId == userId &&
            ((b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now)) ||
             (b.Status == BookingStatus.Confirmed && b.Flights.Any(f => f.Flight.DepartureAt > now))), cancellationToken);
        if (heldOrFlying)
        {
            return true;
        }

        return await _context.Refunds.AnyAsync(r => r.Payment.Booking.UserId == userId &&
            (r.Status == RefundStatus.Pending || r.Status == RefundStatus.Processing), cancellationToken);
    }

    public async Task StageErasureAsync(int userId, string email, int keepTokenId, CancellationToken cancellationToken = default)
    {
        _context.SavedPassengers.RemoveRange(await _context.SavedPassengers.Where(p => p.UserId == userId).ToListAsync(cancellationToken));
        _context.Notifications.RemoveRange(await _context.Notifications.Where(n => n.UserId == userId).ToListAsync(cancellationToken));
        _context.EmailTokens.RemoveRange(await _context.EmailTokens.Where(t => t.UserId == userId && t.Id != keepTokenId).ToListAsync(cancellationToken));

        var tickets = await _context.SupportTickets.Where(t => t.UserId == userId).Include(t => t.Messages).ToListAsync(cancellationToken);
        _context.SupportMessages.RemoveRange(tickets.SelectMany(t => t.Messages));
        _context.SupportTickets.RemoveRange(tickets);

        var normalized = email.Trim().ToLowerInvariant();
        _context.NewsletterSubscribers.RemoveRange(await _context.NewsletterSubscribers.Where(s => s.Email == normalized).ToListAsync(cancellationToken));
    }
}
