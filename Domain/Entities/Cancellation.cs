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

    public Booking Booking { get; set; } = null!;
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
