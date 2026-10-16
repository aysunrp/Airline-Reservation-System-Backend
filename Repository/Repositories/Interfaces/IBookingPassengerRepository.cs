using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IBookingPassengerRepository : IBaseRepository<BookingPassenger>
{
    /// <summary>Every booked passenger with their booking (contact details, status, total), for the admin passengers page (no tracking).</summary>
    Task<IReadOnlyList<BookingPassenger>> GetAllWithBookingsAsync(CancellationToken cancellationToken = default);
}
