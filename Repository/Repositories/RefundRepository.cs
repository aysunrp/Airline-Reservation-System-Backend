using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class RefundRepository : BaseRepository<Refund>, IRefundRepository
{
    public RefundRepository(AppDbContext context) : base(context)
    {
    }

    private IQueryable<Refund> WithDetails() => Set
        .Include(r => r.Payment)
        .Include(r => r.InitiatedByUser)
        .Include(r => r.Cancellation).ThenInclude(c => c.Booking).ThenInclude(b => b.Payments);

    public Task<Refund?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        => WithDetails().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
        => await Set.Where(r => r.PaymentId == paymentId).ToListAsync(cancellationToken);

    public Task<Refund?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default)
        => WithDetails().FirstOrDefaultAsync(r => r.ProviderReference == providerReference, cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetPageAsync(RefundStatus? status, int take, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(r => r.InitiatedByUser)
            .Include(r => r.Cancellation).ThenInclude(c => c.Booking)
            .Where(r => status == null || r.Status == status)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetUnsentStripeRefundsAsync(DateTime createdBefore, int take, CancellationToken cancellationToken = default)
        => await WithDetails()
            .Where(r => r.Status == RefundStatus.Pending
                        && r.ProviderReference == null
                        && r.Payment.Method == PaymentMethod.StripeCheckout
                        && r.CreatedAt < createdBefore)
            .OrderBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
