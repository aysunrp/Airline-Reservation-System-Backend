using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

/// <summary>One row of the admin flight list. Times are UTC; the page shows them at each airport.</summary>
public record AdminFlightRowDto(
    int Id,
    string FlightNumber,
    string OriginCode,
    string OriginCity,
    string DestinationCode,
    string DestinationCity,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    int AircraftId,
    string AircraftModel,
    FlightStatus Status,
    int Stops,
    string FlightType,
    int? FlightScheduleId,
    bool ScheduleActive,
    IReadOnlyDictionary<CabinClass, decimal> Fares,
    int SeatsTotal,
    int SeatsAvailable);

/// <summary>One flight of a journey with a transfer.</summary>
public record AdminJourneyLegDto(
    int FlightId,
    string FlightNumber,
    string OriginCode,
    string DestinationCode,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    int DurationMinutes,
    string AircraftModel);

/// <summary>
/// A journey with a transfer, as the search offers it: its flights in order, the airport where the passenger changes, the wait there and
/// the whole duration. It is not stored: it exists while its flights exist and the transfer rules hold.
/// </summary>
public record AdminJourneyDto(
    string OriginCode,
    string DestinationCode,
    string TransferCode,
    string TransferCity,
    int TransferMinutes,
    int DurationMinutes,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    IReadOnlyDictionary<CabinClass, decimal> Fares,
    IReadOnlyList<AdminJourneyLegDto> Legs);
