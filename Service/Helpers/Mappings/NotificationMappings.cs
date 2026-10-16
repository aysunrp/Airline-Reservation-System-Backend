using Domain.Entities;
using Service.Helpers.DTOs.Notification;

namespace Service.Helpers.Mappings;

public static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification notification)
        => new(notification.Id, notification.Type, notification.Title, notification.Message,
            notification.BookingId, notification.IsRead, notification.CreatedAt);
}
