using Domain.Enums;

namespace Repository.Repositories.Interfaces;

/// <summary>
/// Filters of the admin flight list. Every field is optional. <see cref="Month"/> and <see cref="Year"/> are the UTC departure month and year;
/// <see cref="FlightType"/> is "direct" (no stop) or "withStops" (one flight with stops on the way, as the flight number is the same);
/// <see cref="Active"/> looks at the schedule the flight belongs to (flights made by hand have no schedule and count as active).
/// </summary>
public record AdminFlightFilter(
    string? Search, string? OriginCode, string? DestinationCode, FlightStatus? Status, DateOnly? Date,
    int? Month, int? Year, int? AircraftId, string? FlightType, bool? Active);
