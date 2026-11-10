using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

/// <summary>Admin flight form. Times are UTC.</summary>
public class FlightRequestDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public int AircraftId { get; set; }
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    /// <summary>Base fare per cabin; only cabins the aircraft has can be sold.</summary>
    public Dictionary<CabinClass, decimal> Fares { get; set; } = new();

    /// <summary>Optional link to the schedule the flight belongs to.</summary>
    public int? FlightScheduleId { get; set; }
}
