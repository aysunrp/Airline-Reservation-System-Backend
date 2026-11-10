using Domain.Enums;
using Service.Helpers.DTOs.Notification;

namespace Service.Services.Interfaces;

/// <summary>Notifications page and the unread badge in the account menu. The userId comes from the token.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Removes one of the user's notifications; 404 when it is someone else's or does not exist.</summary>
    Task DeleteAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    /// <summary>Removes all of the user's notifications ("Clear" on the notifications page).</summary>
    Task ClearAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a notification for the user. Does not save; it is committed with the caller's unit of work.</summary>
    Task QueueAsync(int userId, int? bookingId, NotificationType type, string title, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="QueueAsync"/>, for an event that must be told once (a cancellation, a refund reaching a status): when a notification
    /// with this key exists already nothing is added and the answer is false. A unique index backs this up in the database.
    /// </summary>
    Task<bool> QueueOnceAsync(string emailKey, int userId, int? bookingId, NotificationType type, string title, string message, CancellationToken cancellationToken = default);
}
