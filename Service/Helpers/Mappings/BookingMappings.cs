using Domain.Entities;
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
                    cancellation.Refunds.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.Status));
    }

    public static BookingSummaryDto ToSummaryDto(this Booking booking)
        => new(booking.Id, booking.Pnr, booking.Status, booking.TripType, booking.TotalAmount, booking.CreatedAt, booking.ToLegDtos());

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
                f.CabinClass))
            .ToList();

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
                    t.BaggagePrice,
                    t.MealOptionId,
                    t.MealPrice))
                .ToList());

    // Bookings can be opened with only the PNR and a last name, so the full document number is never returned.
    private static string MaskDocumentNumber(string number)
        => number.Length <= 4 ? new string('*', number.Length) : new string('*', number.Length - 4) + number[^4..];
}
