using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class BookingFlight : BaseEntity
{
    public int BookingId { get; set; }
    public int FlightId { get; set; }
    public int LegOrder { get; set; }

    /// <summary>The journey this flight belongs to: 1 is the outbound trip, 2 the return. A journey with a transfer has two or more flights with the same number.</summary>
    public int JourneyNo { get; set; } = 1;
    public CabinClass CabinClass { get; set; }

    public Booking Booking { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
