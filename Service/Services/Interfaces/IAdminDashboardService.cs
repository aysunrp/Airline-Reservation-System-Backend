using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>Admin dashboard: the five newest bookings and the next five departures.</summary>
public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
