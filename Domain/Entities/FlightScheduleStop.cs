using Domain.Common;

namespace Domain.Entities;

/// <summary>An intermediate airport of a schedule, timed from the scheduled departure.</summary>
public class FlightScheduleStop : BaseEntity
{
    public int FlightScheduleId { get; set; }
    public int SequenceNo { get; set; }
    public int AirportId { get; set; }

    /// <summary>Minutes from the scheduled departure until the aircraft lands at this stop.</summary>
    public int ArrivalOffsetMinutes { get; set; }

    public int LayoverMinutes { get; set; }

    public FlightSchedule FlightSchedule { get; set; } = null!;
    public Airport Airport { get; set; } = null!;
}
