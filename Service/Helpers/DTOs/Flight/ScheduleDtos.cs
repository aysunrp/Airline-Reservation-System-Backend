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
    bool IsActive,
    IReadOnlyDictionary<CabinClass, decimal> Fares,
    IReadOnlyList<ScheduleStopDto> Stops);

public record ScheduleStopDto(
    int SequenceNo,
    string AirportCode,
    string City,
    int ArrivalOffsetMinutes,
    int LayoverMinutes);

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

    /// <summary>Base price per cabin for the flights of this schedule; each cabin at most once.</summary>
    public List<ScheduleFareRequestDto> Fares { get; set; } = new();

    /// <summary>Intermediate airports, numbered 1, 2, ... in flying order. Empty for a non-stop schedule.</summary>
    public List<ScheduleStopRequestDto> Stops { get; set; } = new();
}

public class ScheduleFareRequestDto
{
    public CabinClass CabinClass { get; set; }
    public decimal BasePrice { get; set; }
}

public class ScheduleStopRequestDto
{
    public int SequenceNo { get; set; }
    public string AirportCode { get; set; } = string.Empty;

    /// <summary>Minutes from the scheduled departure until landing at the stop.</summary>
    public int ArrivalOffsetMinutes { get; set; }

    public int LayoverMinutes { get; set; }
}
