using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Flight : BaseEntity
{
    public int? FlightScheduleId { get; set; }

    /// <summary>
    /// For flights generated from a schedule: the operating day at the departure airport. Together with
    /// <see cref="FlightScheduleId"/> it is unique, so a schedule can never produce two flights for one day.
    /// Null for flights created by hand.
    /// </summary>
    public DateOnly? ScheduledDate { get; set; }
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
