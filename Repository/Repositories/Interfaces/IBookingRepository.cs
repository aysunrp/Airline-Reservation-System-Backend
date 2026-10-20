using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IBookingRepository : IBaseRepository<Booking>
{
    /// <summary>
    /// Returns a tracked booking with everything the booking details page shows: legs with flight, aircraft and route airports,
    /// passengers with tickets, seats and baggage/meal options, payments, cancellation with refunds and the promo code.
    /// </summary>
    Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default);

    Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default);

    /// <summary>A user's bookings with legs (route airports, aircraft), passenger seats and refunds, newest first (no tracking).</summary>
    Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Admin bookings table: optional PNR fragment, passenger name fragment and status filters, newest first,
    /// with legs, route airports and seats (no tracking).
    /// </summary>
    Task<IReadOnlyList<Booking>> GetForAdminAsync(string? pnr, string? passengerName, BookingStatus? status, int? take = null, CancellationToken cancellationToken = default);

    /// <summary>Accounts with a pending or confirmed booking on the flight (for flight update notifications).</summary>
    Task<IReadOnlyList<int>> GetUserIdsWithActiveBookingsOnFlightAsync(int flightId, CancellationToken cancellationToken = default);

    /// <summary>Unpaid bookings whose seat hold has run out, tracked with their tickets so they can be expired.</summary>
    /// <summary>Tracked bookings whose cancellation still has a Pending or Processing refund, with payments.</summary>
    Task<IReadOnlyList<Booking>> GetWithOpenRefundsAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked confirmed bookings with at least one flight that has landed by <paramref name="now"/>, with legs and tickets.</summary>
    Task<IReadOnlyList<Booking>> GetConfirmedWithLandedFlightsAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Bookings that were paid for and are still valid: Confirmed or Completed.</summary>
    Task<int> CountPaidAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirmed bookings of signed-in users with a flight departing after <paramref name="from"/> and no later than <paramref name="to"/> (no tracking).</summary>
    Task<IReadOnlyList<Booking>> GetConfirmedDepartingBetweenAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default);
}
