using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class BookingFlight : BaseEntity
{
    public int BookingId { get; set; }
    public int FlightId { get; set; }
    public int LegOrder { get; set; }
    public CabinClass CabinClass { get; set; }

    public Booking Booking { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
