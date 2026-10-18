using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>Base price of one cabin on every flight of a schedule.</summary>
public class FlightScheduleFare : BaseEntity
{
    public int FlightScheduleId { get; set; }
    public CabinClass CabinClass { get; set; }
    public decimal BasePrice { get; set; }

    public FlightSchedule FlightSchedule { get; set; } = null!;
}
