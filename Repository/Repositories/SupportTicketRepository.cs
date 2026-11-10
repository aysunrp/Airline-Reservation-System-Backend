using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class SupportTicketRepository : BaseRepository<SupportTicket>, ISupportTicketRepository
{
    public SupportTicketRepository(AppDbContext context) : base(context)
    {
    }

    public Task<SupportTicket?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Set.Include(t => t.User).Include(t => t.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> NumberExistsAsync(string ticketNumber, CancellationToken cancellationToken = default)
        => Set.AnyAsync(t => t.TicketNumber == ticketNumber, cancellationToken);

    public async Task<IReadOnlyList<(SupportTicket Ticket, int MessageCount)>> GetForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastMessageAt).ThenByDescending(t => t.Id)
            .Select(t => new { Ticket = t, Count = t.Messages.Count })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Ticket, r.Count)).ToList();
    }

    public Task<int> CountOpenForUserAsync(int userId, CancellationToken cancellationToken = default)
        => Set.CountAsync(t => t.UserId == userId && t.Status != SupportStatus.Resolved, cancellationToken);

    public Task<SupportTicket?> FindRecentDuplicateAsync(int userId, string subject, DateTime since, CancellationToken cancellationToken = default)
        => Set.Include(t => t.Messages).Where(t => t.UserId == userId && t.Subject == subject && t.CreatedAt >= since)
            .OrderByDescending(t => t.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<(SupportTicket Ticket, int MessageCount)> Items, int Total)> SearchAsync(
        string? search, SupportStatus? status, SupportCategory? category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Include(t => t.User).AsQueryable();
        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }

        if (category is not null)
        {
            query = query.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.TicketNumber.Contains(term) || t.Subject.Contains(term) || (t.BookingPnr != null && t.BookingPnr.Contains(term))
                                     || t.User.Email.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(t => t.LastMessageAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new { Ticket = t, Count = t.Messages.Count })
            .ToListAsync(cancellationToken);
        return (rows.Select(r => (r.Ticket, r.Count)).ToList(), total);
    }
}
