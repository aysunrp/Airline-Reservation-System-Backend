using Domain.Enums;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.DTOs.Reservation;

/// <summary>A row of the admin bookings table; flight, route, date, cabin and seats describe the first flight.</summary>
public record AdminBookingRowDto(
    int Id,
    string Pnr,
    string LeadPassenger,
    int PassengerCount,
    string FlightNumber,
    string Route,
    DateTime DepartureAt,
    CabinClass CabinClass,
    IReadOnlyList<string> Seats,
    decimal TotalAmount,
    BookingStatus Status);

public record AdminDashboardDto(
    IReadOnlyList<AdminBookingRowDto> RecentBookings,
    IReadOnlyList<FlightResultDto> UpcomingFlights);
