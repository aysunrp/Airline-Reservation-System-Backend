using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

/// <summary>
/// Filters of the admin flight list, as the API receives them. Every field is optional. <see cref="Month"/> and <see cref="Year"/> are the UTC
/// departure month and year; <see cref="FlightType"/> is "direct" (no stop) or "withStops"; <see cref="Active"/> looks at the schedule the flight
/// belongs to (flights made by hand have no schedule and count as active); <see cref="Timing"/> is "upcoming" or "past", any other value is ignored.
/// The Service layer turns it into the filter of the data layer, so the API never needs a type from the Repository layer.
/// </summary>
public record AdminFlightFilterDto(
    string? Search, string? OriginCode, string? DestinationCode, FlightStatus? Status, DateOnly? Date,
    int? Month, int? Year, int? AircraftId, string? FlightType, bool? Active, string? Timing = null);
