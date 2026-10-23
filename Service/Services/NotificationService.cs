using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Notification;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId, cancellationToken);
        return notifications.Select(n => n.ToDto()).ToList();
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        => _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);

    public async Task MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId)
        {
            throw new NotFoundException("Notification not found.");
        }

        if (notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        _notificationRepository.Update(notification);
        await _notificationRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unread = await _notificationRepository.GetUnreadTrackedAsync(userId, cancellationToken);
        if (unread.Count == 0)
        {
            return;
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            _notificationRepository.Update(notification);
        }

        await _notificationRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId)
        {
            throw new NotFoundException("Notification not found.");
        }

        _notificationRepository.Delete(notification);
        await _notificationRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(int userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationRepository.GetByUserIdTrackedAsync(userId, cancellationToken);
        if (notifications.Count == 0)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            _notificationRepository.Delete(notification);
        }

        await _notificationRepository.SaveChangesAsync(cancellationToken);
    }

    public Task QueueAsync(int userId, int? bookingId, NotificationType type, string title, string message, CancellationToken cancellationToken = default)
        => _notificationRepository.AddAsync(new Notification
        {
            UserId = userId,
            BookingId = bookingId,
            Type = type,
            Title = title,
            Message = message
        }, cancellationToken);
}
