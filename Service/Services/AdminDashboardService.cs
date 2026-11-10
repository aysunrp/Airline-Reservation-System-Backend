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
    private readonly IRouteRepository _routeRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IAdminPassengerService _passengerService;
    private readonly TimeProvider _timeProvider;

    public AdminDashboardService(
        IBookingRepository bookingRepository,
        IFlightRepository flightRepository,
        IRouteRepository routeRepository,
        IAircraftRepository aircraftRepository,
        IFlightSeatRepository flightSeatRepository,
        IAdminPassengerService passengerService,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _flightRepository = flightRepository;
        _routeRepository = routeRepository;
        _aircraftRepository = aircraftRepository;
        _flightSeatRepository = flightSeatRepository;
        _passengerService = passengerService;
        _timeProvider = timeProvider;
    }

    public async Task<AdminDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var bookings = await _bookingRepository.GetForAdminAsync(null, null, null, RowsPerTable, cancellationToken);
        var flights = await _flightRepository.GetUpcomingAsync(now, RowsPerTable, cancellationToken);

        var stats = new AdminDashboardStatsDto(
            await _flightRepository.CountAsync(cancellationToken),
            await _routeRepository.CountActiveAsync(cancellationToken),
            await _aircraftRepository.CountAsync(cancellationToken),
            await _bookingRepository.CountPaidAsync(cancellationToken),
            await _flightSeatRepository.CountAvailableOnUpcomingFlightsAsync(now, cancellationToken),
            (await _passengerService.GetAllAsync(null, null, cancellationToken)).Count);

        return new AdminDashboardDto(
            bookings.Select(b => b.ToAdminRowDto()).ToList(),
            flights.Select(f => f.ToResultDto()).ToList(),
            stats);
    }
}
