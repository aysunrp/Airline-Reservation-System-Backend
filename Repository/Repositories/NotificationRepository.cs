using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class NotificationRepository : BaseRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        => Set.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadTrackedAsync(int userId, CancellationToken cancellationToken = default)
        => await Set
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetByUserIdTrackedAsync(int userId, CancellationToken cancellationToken = default)
        => await Set
            .Where(n => n.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<int>> GetBookingIdsWithTypeAsync(NotificationType type, IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken = default)
        => (await Set.AsNoTracking()
            .Where(n => n.Type == type && n.BookingId != null && bookingIds.Contains(n.BookingId.Value))
            .Select(n => n.BookingId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

    public async Task<IReadOnlyList<Notification>> GetDueForEmailAsync(DateTime createdAfter, int maxAttempts, int take, CancellationToken cancellationToken = default)
        => await Set
            .Include(n => n.User)
            .Include(n => n.Booking)
            .Where(n => n.EmailSentAt == null
                        && n.EmailAttempts < maxAttempts
                        && n.CreatedAt > createdAfter
                        && n.User.EmailVerifiedAt != null)
            .OrderBy(n => n.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<bool> EmailKeyExistsAsync(string emailKey, CancellationToken cancellationToken = default)
        => Set.AnyAsync(n => n.EmailKey == emailKey, cancellationToken);
}
