using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IBookingRepository : IBaseRepository<Booking>
{
    /// <summary>
    /// Returns a tracked booking with everything the booking details page shows: legs with flight and route airports,
    /// passengers with tickets and seats, payments, cancellation with refunds and the promo code.
    /// </summary>
    Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default);

    Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default);

    /// <summary>A user's bookings with their flight legs and route airports, newest first (no tracking).</summary>
    Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Accounts with a pending or confirmed booking on the flight (for flight update notifications).</summary>
    Task<IReadOnlyList<int>> GetUserIdsWithActiveBookingsOnFlightAsync(int flightId, CancellationToken cancellationToken = default);

    /// <summary>Unpaid bookings whose seat hold has run out, tracked with their tickets so they can be expired.</summary>
    Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default);
}
