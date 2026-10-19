using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Passenger;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminPassengerService : IAdminPassengerService
{
    private readonly IBookingPassengerRepository _passengerRepository;

    public AdminPassengerService(IBookingPassengerRepository passengerRepository)
    {
        _passengerRepository = passengerRepository;
    }

    public async Task<IReadOnlyList<AdminPassengerDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        var travellers = await GetTravellersAsync(cancellationToken);

        return travellers
            .Select(t => t.Passenger)
            .Where(p => isActive is null || p.IsActive == isActive)
            .Where(p => string.IsNullOrEmpty(term)
                        || p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || p.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || p.DocumentNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.FullName)
            .ToList();
    }

    public async Task<AdminPassengerDetailsDto> GetDetailsAsync(DocumentType documentType, string documentNumber, CancellationToken cancellationToken = default)
    {
        var number = documentNumber.Trim().ToUpperInvariant();
        var traveller = (await GetTravellersAsync(cancellationToken))
            .FirstOrDefault(t => t.Passenger.DocumentType == documentType && t.Passenger.DocumentNumber == number);

        return traveller.Passenger is null
            ? throw new NotFoundException("Passenger not found.")
            : new AdminPassengerDetailsDto(traveller.Passenger, traveller.Bookings);
    }

    private static bool CountsAsTrip(Booking booking) => booking.Status is BookingStatus.Confirmed or BookingStatus.Completed;

    private async Task<List<(AdminPassengerDto Passenger, List<AdminPassengerBookingDto> Bookings)>> GetTravellersAsync(CancellationToken cancellationToken)
    {
        var records = await _passengerRepository.GetAllWithBookingsAsync(cancellationToken);

        return records
            .GroupBy(p => (p.DocumentType, p.DocumentNumber))
            .Select(group =>
            {
                var latest = group.OrderByDescending(p => p.Booking.CreatedAt).First();
                var bookings = group
                    .Select(p => p.Booking)
                    .DistinctBy(b => b.Id)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();
                var trips = bookings.Count(CountsAsTrip);

                var passenger = new AdminPassengerDto(
                    latest.FullName(),
                    latest.Booking.ContactEmail,
                    latest.Booking.ContactPhone,
                    latest.NationalityCode,
                    latest.DocumentType,
                    latest.DocumentNumber,
                    trips,
                    trips > 0);

                return (passenger, bookings.Select(b => new AdminPassengerBookingDto(b.Pnr, b.Status, b.TotalAmount, b.CreatedAt)).ToList());
            })
            .ToList();
    }
}
