using Domain.Common;

namespace Domain.Entities;

public class Cancellation : BaseEntity
{
    public int BookingId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal RefundAmount { get; set; }

    /// <summary>The page language when the booking was cancelled (en, az, ru); the cancellation and refund e-mails are written in it.</summary>
    public string? Language { get; set; }

    public Booking Booking { get; set; } = null!;
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
