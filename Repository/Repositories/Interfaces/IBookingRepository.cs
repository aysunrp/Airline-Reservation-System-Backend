using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IBookingRepository : IBaseRepository<Booking>
{
    /// <summary>Returns a tracked booking with legs, passengers, tickets, payments and cancellation.</summary>
    Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default);

    Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default);

    /// <summary>A user's bookings with their flight legs, newest first (no tracking).</summary>
    Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
