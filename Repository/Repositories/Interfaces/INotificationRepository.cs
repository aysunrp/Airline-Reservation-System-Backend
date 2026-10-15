using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface INotificationRepository : IBaseRepository<Notification>
{
    /// <summary>A user's notifications, newest first (no tracking).</summary>
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
}
