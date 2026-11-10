using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>A customer's support request with its conversation. Only its owner (and staff) can read it.</summary>
public class SupportTicket : BaseEntity
{
    /// <summary>Unique, shown to the customer and in e-mails, e.g. SUP-7K3QXM.</summary>
    public string TicketNumber { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public SupportCategory Category { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>Optional booking reference the request is about (not checked against the bookings of the account).</summary>
    public string? BookingPnr { get; set; }

    public SupportStatus Status { get; set; } = SupportStatus.Open;

    /// <summary>When the last message was written; the list is ordered by it.</summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public List<SupportMessage> Messages { get; set; } = new();
}

public class SupportMessage : BaseEntity
{
    public int TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;

    public SupportAuthorType AuthorType { get; set; }

    /// <summary>The customer or the staff account that wrote it.</summary>
    public int AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;
}
