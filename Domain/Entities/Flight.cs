using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Flight : BaseEntity
{
    public int? FlightScheduleId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public int RouteId { get; set; }
    public int AircraftId { get; set; }
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    public FlightSchedule? FlightSchedule { get; set; }
    public Route Route { get; set; } = null!;
    public Aircraft Aircraft { get; set; } = null!;
    public ICollection<FlightSegment> Segments { get; set; } = new List<FlightSegment>();
    public ICollection<FlightFare> Fares { get; set; } = new List<FlightFare>();
    public ICollection<FlightSeat> Seats { get; set; } = new List<FlightSeat>();
}
