using Service.Helpers.DTOs.Notification;

namespace Service.Services.Interfaces;

/// <summary>Notifications page and the unread badge in the account menu. The userId comes from the token.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
}
