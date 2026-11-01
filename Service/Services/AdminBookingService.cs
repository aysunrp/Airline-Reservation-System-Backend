using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminBookingService : IAdminBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationService _cancellationService;

    public AdminBookingService(IBookingRepository bookingRepository, ICancellationService cancellationService)
    {
        _bookingRepository = bookingRepository;
        _cancellationService = cancellationService;
    }

    public async Task<IReadOnlyList<AdminBookingRowDto>> GetAllAsync(string? pnr = null, string? passengerName = null, BookingStatus? status = null, int take = 200, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetForAdminAsync(pnr?.Trim().ToUpperInvariant(), passengerName, status, Math.Clamp(take, 1, 500), cancellationToken);
        return bookings.Select(b => b.ToAdminRowDto()).ToList();
    }

    public async Task<BookingDto> GetDetailsAsync(string pnr, CancellationToken cancellationToken = default)
        => (await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(pnr), cancellationToken))?.ToDto()
           ?? throw new NotFoundException("Bron tapılmadı.");

    public Task<BookingDto> CancelAsync(string pnr, string? reason = null, int? adminUserId = null, CancellationToken cancellationToken = default)
        => _cancellationService.CancelByAdminAsync(pnr, reason, adminUserId, cancellationToken);
}
