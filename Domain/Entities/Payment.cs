using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Payment : BaseEntity
{
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ProviderReference { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardBrand { get; set; }
    public DateTime? PaidAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
