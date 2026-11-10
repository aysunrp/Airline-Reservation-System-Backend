using Domain.Enums;

namespace Repository.Repositories.Interfaces;

/// <summary>
/// What "my trips" needs of one booking, read with narrow projections instead of loading whole entities with their navigations:
/// the list is shown on every visit, so it must stay cheap for the database (small rows, no wide sort).
/// </summary>
public sealed record BookingSummaryRow(
    int Id,
    string Pnr,
    BookingStatus Status,
    TripType TripType,
    decimal TotalAmount,
    DateTime CreatedAt,
    int PassengerCount,
    decimal? CancellationRefundAmount,
    RefundStatus? RefundStatus,
    string? RefundCurrency,
    bool RefundIsDemo,
    IReadOnlyList<BookingLegRow> Legs);

public sealed record BookingLegRow(
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
    IReadOnlyList<string> Seats,
    int JourneyNo = 1);
