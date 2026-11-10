using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class BookingPassengerRepository : BaseRepository<BookingPassenger>, IBookingPassengerRepository
{
    public BookingPassengerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<BookingPassenger>> GetAllWithBookingsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(p => p.Booking)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
}
