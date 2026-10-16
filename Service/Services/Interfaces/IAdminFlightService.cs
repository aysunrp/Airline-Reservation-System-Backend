using Domain.Enums;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seat;

namespace Service.Services.Interfaces;

/// <summary>Admin "Flights" and "Seat Inventory" pages.</summary>
public interface IAdminFlightService
{
    /// <summary>Optionally filtered by flight number, city or aircraft model, and by status.</summary>
    Task<IReadOnlyList<FlightResultDto>> GetAllAsync(string? search = null, FlightStatus? status = null, CancellationToken cancellationToken = default);

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
