using Domain.Enums;

namespace Service.Helpers.DTOs.Notification;

public record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Message,
    int? BookingId,
    bool IsRead,
    DateTime CreatedAt,
    int? SupportTicketId = null);
