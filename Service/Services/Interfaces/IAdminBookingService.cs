using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>Admin "Bookings" page.</summary>
public interface IAdminBookingService
{
    /// <summary>Filters by PNR fragment, passenger name fragment and status; newest first.</summary>
    Task<IReadOnlyList<AdminBookingRowDto>> GetAllAsync(string? pnr = null, string? passengerName = null, BookingStatus? status = null, int take = 200, CancellationToken cancellationToken = default);

    Task<BookingDto> GetDetailsAsync(string pnr, CancellationToken cancellationToken = default);

    Task<BookingDto> CancelAsync(string pnr, string? reason = null, CancellationToken cancellationToken = default);
}
