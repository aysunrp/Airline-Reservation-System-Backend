using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminDashboardService : IAdminDashboardService
{
    // The dashboard tables show five rows each.
    private const int RowsPerTable = 5;

    private readonly IBookingRepository _bookingRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly TimeProvider _timeProvider;

    public AdminDashboardService(IBookingRepository bookingRepository, IFlightRepository flightRepository, TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _flightRepository = flightRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AdminDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetForAdminAsync(null, null, null, RowsPerTable, cancellationToken);
        var flights = await _flightRepository.GetUpcomingAsync(_timeProvider.GetUtcNow().UtcDateTime, RowsPerTable, cancellationToken);

        return new AdminDashboardDto(
            bookings.Select(b => b.ToAdminRowDto()).ToList(),
            flights.Select(f => f.ToResultDto()).ToList());
    }
}
