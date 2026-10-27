using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Booking : BaseEntity
{
    public string Pnr { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public TripType TripType { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal BaseFare { get; set; }
    public decimal BaggageTotal { get; set; }
    public decimal MealTotal { get; set; }
    public decimal TaxesTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int? PromoCodeId { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Two changes to the same booking at once (a Stripe confirmation and the expiry job, say) cannot both win.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public User? User { get; set; }
    public PromoCode? PromoCode { get; set; }
    public ICollection<BookingFlight> Flights { get; set; } = new List<BookingFlight>();
    public ICollection<BookingPassenger> Passengers { get; set; } = new List<BookingPassenger>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Cancellation? Cancellation { get; set; }
}
