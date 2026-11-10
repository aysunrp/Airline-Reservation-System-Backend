using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IBookingService
{
    /// <summary>
    /// Creates a pending booking: validates flights, passengers and seats, holds the seats for the configured time
    /// and prices the booking. Payment confirms it. userId is null for guest bookings.
    /// </summary>
    Task<BookingDto> CreateBookingAsync(int? userId, CreateBookingRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Payment page summary: prices the same request the booking would be created from (fares by passenger type,
    /// baggage, meals, tax and promo code) without holding seats or saving anything.
    /// </summary>
    Task<PriceBreakdownDto> QuoteAsync(CreateBookingRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Manage booking: finds a booking by its reference and the last name of one of its passengers.</summary>
    Task<BookingDto> GetByPnrAsync(string pnr, string lastName, CancellationToken cancellationToken = default);

    /// <summary>My Trips: the signed-in user's bookings, newest first.</summary>
    Task<IReadOnlyList<BookingSummaryDto>> GetMyBookingsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>One of the signed-in user's own bookings by reference (My Trips details); 404 when it belongs to someone else.</summary>
    Task<BookingDto> GetMyBookingAsync(int userId, string pnr, CancellationToken cancellationToken = default);
}
