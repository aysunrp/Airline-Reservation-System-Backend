using Domain.Common;

namespace Domain.Entities;

public class FlightSegment : BaseEntity
{
    public int FlightId { get; set; }
    public int SequenceNo { get; set; }
    public int OriginAirportId { get; set; }
    public int DestinationAirportId { get; set; }
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    public int? LayoverMinutes { get; set; }

    public Flight Flight { get; set; } = null!;
    public Airport Origin { get; set; } = null!;
    public Airport Destination { get; set; } = null!;
}
