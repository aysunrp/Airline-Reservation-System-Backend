using Domain.Common;

namespace Domain.Entities;

public class Route : BaseEntity
{
    public int OriginAirportId { get; set; }
    public int DestinationAirportId { get; set; }
    public int DistanceKm { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public Airport Origin { get; set; } = null!;
    public Airport Destination { get; set; } = null!;
}
