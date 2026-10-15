using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class FlightSchedule : BaseEntity
{
    public string FlightNumber { get; set; } = string.Empty;
    public int RouteId { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly ArrivalTime { get; set; }
    public int ArrivalDayOffset { get; set; }
    public ScheduleDays DaysOfWeek { get; set; } = ScheduleDays.Everyday;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public int? DefaultAircraftId { get; set; }
    public bool IsActive { get; set; } = true;

    public Route Route { get; set; } = null!;
    public Aircraft? DefaultAircraft { get; set; }
}
