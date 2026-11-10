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

/// <summary>
/// The numbers above the dashboard tables. Flights, routes and aircraft are all of them; ActiveRoutes only counts routes that are on;
/// TotalBookings counts paid bookings (Confirmed or Completed); AvailableSeats are the seats still for sale on flights that have not
/// departed; Passengers are the distinct travellers (grouped by travel document) as on the passengers page.
/// </summary>
public record AdminDashboardStatsDto(int TotalFlights, int ActiveRoutes, int Aircraft, int TotalBookings, int AvailableSeats, int Passengers);

public record AdminDashboardDto(
    IReadOnlyList<AdminBookingRowDto> RecentBookings,
    IReadOnlyList<FlightResultDto> UpcomingFlights,
    AdminDashboardStatsDto Stats);
