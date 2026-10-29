using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Notification;

namespace Service.Helpers.Mappings;

public static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification notification)
        => new(notification.Id, notification.Type, notification.Title, notification.Message,
            notification.BookingId, notification.IsRead, notification.CreatedAt, SupportTicketIdOf(notification));

    /// <summary>The ticket a support notification is about: its event key is "support:{ticketId}:...". Opening the ticket still checks who owns it.</summary>
    private static int? SupportTicketIdOf(Notification notification)
    {
        if (notification.Type is not (NotificationType.SupportUpdate or NotificationType.SupportStaff))
        {
            return null;
        }

        var parts = notification.EmailKey?.Split(':');
        return parts is { Length: > 2 } && parts[0] == "support" && int.TryParse(parts[1], out var id) && id > 0 ? id : null;
    }
}
