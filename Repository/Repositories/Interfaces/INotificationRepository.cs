using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface INotificationRepository : IBaseRepository<Notification>
{
    /// <summary>A user's notifications, newest first (no tracking).</summary>
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>A user's unread notifications, tracked so they can be marked as read.</summary>
    Task<IReadOnlyList<Notification>> GetUnreadTrackedAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>All of a user's notifications, tracked so they can be deleted.</summary>
    Task<IReadOnlyList<Notification>> GetByUserIdTrackedAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The bookings among <paramref name="bookingIds"/> that already have a notification of this type.</summary>
    Task<IReadOnlySet<int>> GetBookingIdsWithTypeAsync(NotificationType type, IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken = default);
}
