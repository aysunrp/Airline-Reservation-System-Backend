using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

/// <summary>One-way = 1 leg, round trip = 2 legs, multi-city = one leg per city pair.</summary>
public class FlightSearchRequestDto
{
    public List<FlightSearchLegDto> Legs { get; set; } = new();

    /// <summary>When set, only flights selling this cabin are returned.</summary>
    public CabinClass? CabinClass { get; set; }

    /// <summary>Travellers who need a seat (infants on a lap are not counted); flights with fewer free seats are hidden.</summary>
    public int Passengers { get; set; } = 1;
}

public class FlightSearchLegDto
{
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

public record FlightSearchLegResultDto(
    int LegIndex,
    string OriginCode,
    string DestinationCode,
    DateOnly Date,
    IReadOnlyList<FlightResultDto> Flights);

public record FlightStopDto(string AirportCode, int LayoverMinutes);

/// <summary>One flown leg of a flight, in UTC; a non-stop flight has one, a flight with stops has one per hop.</summary>
public record FlightSegmentDto(int SequenceNo, string OriginCode, string DestinationCode, DateTime DepartureAt, DateTime ArrivalAt);

public record FlightResultDto(
    int Id,
    string FlightNumber,
    string OriginCode,
    string OriginCity,
    string DestinationCode,
    string DestinationCity,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    int DurationMinutes,
    int Stops,
    IReadOnlyList<FlightStopDto> Via,
    int AircraftId,
    string AircraftModel,
    FlightStatus Status,
    IReadOnlyDictionary<CabinClass, decimal> Fares,
    IReadOnlyList<FlightSegmentDto> Segments);
