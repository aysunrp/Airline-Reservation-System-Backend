using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Mappings;

public static class BookingMappings
{
    /// <summary>Expects the graph loaded by IBookingRepository.GetByPnrAsync.</summary>
    public static BookingDto ToDto(this Booking booking)
    {
        var price = new PriceBreakdownDto(
            booking.BaseFare,
            booking.BaggageTotal,
            booking.MealTotal,
            booking.TaxesTotal,
            booking.BaseFare + booking.BaggageTotal + booking.MealTotal + booking.TaxesTotal,
            booking.PromoCode?.Code,
            booking.PromoCode?.DiscountPercent ?? 0,
            booking.DiscountAmount,
            booking.TotalAmount);

        var latestPayment = booking.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var cancellation = booking.Cancellation;

        return new BookingDto(
            booking.Id,
            booking.Pnr,
            booking.Status,
            booking.TripType,
            booking.ContactEmail,
            booking.ContactPhone,
            booking.CreatedAt,
            booking.ExpiresAt,
            price,
            latestPayment?.Status,
            booking.ToLegDtos(),
            booking.Passengers.OrderBy(p => p.Id).Select(p => p.ToDto()).ToList(),
            cancellation is null
                ? null
                : new BookingCancellationDto(
                    cancellation.RequestedAt,
                    cancellation.AmountPaid,
                    cancellation.FeeAmount,
                    cancellation.RefundAmount,
                    booking.LatestRefundStatus()));
    }

    /// <summary>Expects legs with route airports and passengers with tickets and seats to be loaded.</summary>
    public static AdminBookingRowDto ToAdminRowDto(this Booking booking)
    {
        var firstLeg = booking.Flights.OrderBy(f => f.LegOrder).First();
        var passengers = booking.Passengers.OrderBy(p => p.Id).ToList();
        var lead = passengers.FirstOrDefault(p => p.PassengerType == PassengerType.Adult) ?? passengers.First();

        var seats = booking.SeatLabels(firstLeg);

        return new AdminBookingRowDto(
            booking.Id,
            booking.Pnr,
            $"{lead.FirstName} {lead.LastName}",
            passengers.Count,
            firstLeg.Flight.FlightNumber,
            $"{firstLeg.Flight.Route.Origin.City} → {firstLeg.Flight.Route.Destination.City}",
            firstLeg.Flight.DepartureAt,
            firstLeg.CabinClass,
            seats,
            booking.TotalAmount,
            booking.Status);
    }

    /// <summary>Expects legs with route airports and aircraft, passengers with tickets and seats, and refunds to be loaded.</summary>
    public static BookingSummaryDto ToSummaryDto(this Booking booking)
        => new(booking.Id, booking.Pnr, booking.Status, booking.TripType, booking.TotalAmount, booking.CreatedAt,
            booking.Passengers.Count, booking.LatestRefundStatus(), booking.ToLegDtos());

    private static IReadOnlyList<BookingLegDto> ToLegDtos(this Booking booking)
        => booking.Flights
            .OrderBy(f => f.LegOrder)
            .Select(f => new BookingLegDto(
                f.LegOrder,
                f.FlightId,
                f.Flight.FlightNumber,
                f.Flight.Route.Origin.Code,
                f.Flight.Route.Origin.City,
                f.Flight.Route.Destination.Code,
                f.Flight.Route.Destination.City,
                f.Flight.DepartureAt,
                f.Flight.ArrivalAt,
                f.CabinClass,
                f.Flight.Aircraft.Model,
                booking.SeatLabels(f)))
            .ToList();

    // Compared by id: no-tracking queries do not link tickets to legs loaded through another include.
    private static IReadOnlyList<string> SeatLabels(this Booking booking, BookingFlight leg)
        => booking.Passengers
            .OrderBy(p => p.Id)
            .SelectMany(p => p.Tickets)
            .Where(t => t.BookingFlightId == leg.Id && t.FlightSeat is not null)
            .Select(t => $"{t.FlightSeat!.AircraftSeat.RowNumber}{t.FlightSeat.AircraftSeat.Letter}")
            .ToList();

    private static RefundStatus? LatestRefundStatus(this Booking booking)
        => booking.Cancellation?.Refunds.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.Status;

    private static BookingPassengerDto ToDto(this BookingPassenger passenger)
        => new(
            passenger.Id,
            passenger.PassengerType,
            passenger.FirstName,
            passenger.LastName,
            passenger.DateOfBirth,
            passenger.Gender,
            passenger.NationalityCode,
            passenger.DocumentType,
            MaskDocumentNumber(passenger.DocumentNumber),
            passenger.Tickets
                .OrderBy(t => t.BookingFlight.LegOrder)
                .Select(t => new TicketDto(
                    t.BookingFlight.LegOrder,
                    t.TicketNumber,
                    t.FlightSeat is null ? null : $"{t.FlightSeat.AircraftSeat.RowNumber}{t.FlightSeat.AircraftSeat.Letter}",
                    t.Status,
                    t.FareAmount,
                    t.BaggageOptionId,
                    t.BaggageOption?.Name,
                    t.BaggagePrice,
                    t.MealOptionId,
                    t.MealOption?.Name,
                    t.MealPrice))
                .ToList());

    // Bookings can be opened with only the PNR and a last name, so the full document number is never returned.
    private static string MaskDocumentNumber(string number)
        => number.Length <= 4 ? new string('*', number.Length) : new string('*', number.Length - 4) + number[^4..];
}
