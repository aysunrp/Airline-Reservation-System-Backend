using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class FlightFare : BaseEntity
{
    public int FlightId { get; set; }
    public CabinClass CabinClass { get; set; }
    public decimal BasePrice { get; set; }

    public Flight Flight { get; set; } = null!;
}
