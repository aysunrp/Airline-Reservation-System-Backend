using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.DTOs.Passenger;

/// <summary>
/// A traveller on the admin passengers page. Bookings of the same person are grouped by travel document;
/// name and contact details come from the most recent booking. Trips counts confirmed or completed bookings,
/// and a traveller is active when they have at least one such trip.
/// </summary>
public record AdminPassengerDto(
    string FullName,
    string Email,
    string Phone,
    string NationalityCode,
    DocumentType DocumentType,
    string DocumentNumber,
    int Trips,
    bool IsActive);

public record AdminPassengerDetailsDto(AdminPassengerDto Passenger, IReadOnlyList<AdminPassengerBookingDto> Bookings);

public record AdminPassengerBookingDto(string Pnr, BookingStatus Status, decimal TotalAmount, DateTime CreatedAt);
