using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Payment?> GetWithBookingAsync(int id, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Include(p => p.Booking).ThenInclude(b => b.Passengers)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> GetByCheckoutSessionIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => Set.Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == checkoutSessionId, cancellationToken);
}
