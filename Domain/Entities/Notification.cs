using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public int? BookingId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    /// <summary>When the notification was sent to the account's e-mail address; null while it has not been sent.</summary>
    public DateTime? EmailSentAt { get; set; }

    /// <summary>How many times sending it failed; after a few tries it is left alone, the in-app notification stays.</summary>
    public int EmailAttempts { get; set; }

    /// <summary>
    /// The event this notification stands for (for example "refund:12:1:completed"). The same event is never notified twice, however
    /// often Stripe reports it; null for notifications that do not need it.
    /// </summary>
    public string? EmailKey { get; set; }

    public User User { get; set; } = null!;
    public Booking? Booking { get; set; }
}
