using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

public record ScheduleDto(
    int Id,
    string FlightNumber,
    int RouteId,
    string RouteCode,
    string OriginCity,
    string DestinationCity,
    int? DefaultAircraftId,
    string? AircraftModel,
    TimeOnly DepartureTime,
    TimeOnly ArrivalTime,
    int ArrivalDayOffset,
    ScheduleDays DaysOfWeek,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive);

public class ScheduleRequestDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public int RouteId { get; set; }
    public int? DefaultAircraftId { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly ArrivalTime { get; set; }

    /// <summary>1 when the flight lands the next day (e.g. 16:45 → 00:20).</summary>
    public int ArrivalDayOffset { get; set; }

    public ScheduleDays DaysOfWeek { get; set; } = ScheduleDays.Everyday;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}
