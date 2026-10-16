using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface INotificationRepository : IBaseRepository<Notification>
{
    /// <summary>A user's notifications, newest first (no tracking).</summary>
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>A user's unread notifications, tracked so they can be marked as read.</summary>
    Task<IReadOnlyList<Notification>> GetUnreadTrackedAsync(int userId, CancellationToken cancellationToken = default);
}
