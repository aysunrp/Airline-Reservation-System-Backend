using Domain.Enums;

namespace Service.Helpers.DTOs.Reservation;

public record BookingLegDto(
    int LegOrder,
    int FlightId,
    string FlightNumber,
    string OriginCode,
    string OriginCity,
    string DestinationCode,
    string DestinationCity,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    CabinClass CabinClass,
    string AircraftModel,
    IReadOnlyList<string> Seats);

public record TicketDto(
    int LegOrder,
    string TicketNumber,
    string? Seat,
    TicketStatus Status,
    decimal FareAmount,
    int? BaggageOptionId,
    string? BaggageName,
    decimal BaggagePrice,
    int? MealOptionId,
    string? MealName,
    decimal MealPrice);

public record BookingPassengerDto(
    int Id,
    PassengerType PassengerType,
    string Title,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string NationalityCode,
    DocumentType DocumentType,
    string DocumentNumberMasked,
    IReadOnlyList<TicketDto> Tickets);

public record BookingCancellationDto(
    DateTime RequestedAt,
    decimal AmountPaid,
    decimal FeeAmount,
    decimal RefundAmount,
    RefundStatus? RefundStatus);

public record BookingDto(
    int Id,
    string Pnr,
    BookingStatus Status,
    TripType TripType,
    string ContactEmail,
    string ContactPhone,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    PriceBreakdownDto Price,
    PaymentStatus? PaymentStatus,
    IReadOnlyList<BookingLegDto> Legs,
    IReadOnlyList<BookingPassengerDto> Passengers,
    BookingCancellationDto? Cancellation);

/// <summary>A row on the My Trips page.</summary>
public record BookingSummaryDto(
    int Id,
    string Pnr,
    BookingStatus Status,
    TripType TripType,
    decimal TotalAmount,
    DateTime CreatedAt,
    int PassengerCount,
    RefundStatus? RefundStatus,
    IReadOnlyList<BookingLegDto> Legs);
