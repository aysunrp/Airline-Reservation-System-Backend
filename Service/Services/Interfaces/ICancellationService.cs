using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface ICancellationService
{
    /// <summary>Cancel modal: amount paid, cancellation fee and estimated refund before the user confirms.</summary>
    Task<CancellationQuoteDto> GetQuoteAsync(string pnr, string lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the booking for good: tickets are cancelled, seats released and, for a paid booking,
    /// a refund request is recorded (Pending, or NotEligible when the fare rules allow no refund).
    /// </summary>
    Task<BookingDto> CancelAsync(CancelBookingRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Admin "Cancel Booking": same rules and refund handling, without the last-name check.</summary>
    Task<BookingDto> CancelByAdminAsync(string pnr, string? reason, CancellationToken cancellationToken = default);
}
