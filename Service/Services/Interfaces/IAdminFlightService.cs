using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seat;

namespace Service.Services.Interfaces;

/// <summary>Admin "Flights" and "Seat Inventory" pages.</summary>
public interface IAdminFlightService
{
    /// <summary>Optionally filtered by flight number, city or aircraft model, and by status.</summary>
    Task<IReadOnlyList<FlightResultDto>> GetAllAsync(string? search = null, FlightStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>One page of flights, oldest departure first. take is limited to 1-500.</summary>
    Task<IReadOnlyList<FlightResultDto>> GetPageAsync(string? search, FlightStatus? status, DateOnly? date, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>One page of the admin list with every filter applied (origin, destination, month, year, aircraft, type, active), with seat counts.</summary>
    Task<IReadOnlyList<AdminFlightRowDto>> GetFilteredPageAsync(AdminFlightFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// The journeys with a transfer from one airport to another on the departure days in [from, to] (at most 62 days), as the search builds them,
    /// with no seat filter: the admin sees every journey, whether or not it still has seats.
    /// </summary>
    Task<IReadOnlyList<AdminJourneyDto>> GetJourneysAsync(string originCode, string destinationCode, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Creates the flight with its fares, a direct segment and a seat inventory built from the aircraft's seats.</summary>
    Task<FlightResultDto> CreateAsync(FlightRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Route and aircraft can only change while the flight has no bookings. Status or time changes are announced
    /// to account holders with an active booking on the flight.
    /// </summary>
    Task<FlightResultDto> UpdateAsync(int id, FlightRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Only flights without bookings can be deleted; others should be cancelled.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<SeatInventoryDto> GetSeatInventoryAsync(int flightId, CabinClass? cabinClass = null, CancellationToken cancellationToken = default);

    /// <summary>Blocks a free seat or unblocks a blocked one.</summary>
    Task<InventorySeatDto> SetSeatBlockedAsync(int flightId, int flightSeatId, bool blocked, CancellationToken cancellationToken = default);
}
