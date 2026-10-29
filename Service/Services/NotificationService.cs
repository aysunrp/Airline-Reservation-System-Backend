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
    private readonly IAdminAlertService? _adminAlerts;

    public NotificationService(INotificationRepository notificationRepository, IAdminAlertService? adminAlerts = null)
    {
        _notificationRepository = notificationRepository;
        _adminAlerts = adminAlerts;
    }

    /// <summary>
    /// The events the admin is told about: a confirmed booking, a confirmed payment, a cancellation, and each stage of a refund. They are the same
    /// events the customer is notified about, so the admin hears only what the backend (and Stripe, for payments and refunds) has confirmed.
    /// </summary>
    private async Task AlertAdminAsync(NotificationType type, int? bookingId, string key, string message, CancellationToken cancellationToken)
    {
        if (_adminAlerts is null)
        {
            return;
        }

        var subject = type switch
        {
            NotificationType.BookingConfirmed => "Booking confirmed",
            NotificationType.PaymentSuccessful => "Payment confirmed",
            NotificationType.BookingCancelled => "Booking cancelled",
            NotificationType.RefundPending => "Refund processing",
            NotificationType.RefundCompleted => "Refund succeeded",
            NotificationType.RefundFailed => "Refund FAILED",
            _ => null
        };
        if (subject is null)
        {
            return;
        }

        await _adminAlerts.QueueAsync("admin:" + key, subject, message + (bookingId is null ? string.Empty : $"\n\nBooking id: {bookingId}"), cancellationToken);
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

    public async Task QueueAsync(int userId, int? bookingId, NotificationType type, string title, string message, CancellationToken cancellationToken = default)
    {
        await _notificationRepository.AddAsync(new Notification
        {
            UserId = userId,
            BookingId = bookingId,
            Type = type,
            Title = title,
            Message = message
        }, cancellationToken);

        if (bookingId is not null)
        {
            await AlertAdminAsync(type, bookingId, $"booking:{bookingId}:{type}", message, cancellationToken);
        }
    }

    public async Task<bool> QueueOnceAsync(string emailKey, int userId, int? bookingId, NotificationType type, string title, string message, CancellationToken cancellationToken = default)
    {
        if (await _notificationRepository.EmailKeyExistsAsync(emailKey, cancellationToken))
        {
            return false;
        }

        await _notificationRepository.AddAsync(new Notification
        {
            UserId = userId,
            BookingId = bookingId,
            Type = type,
            Title = title,
            Message = message,
            EmailKey = emailKey
        }, cancellationToken);
        await AlertAdminAsync(type, bookingId, emailKey, message, cancellationToken);
        return true;
    }
}
