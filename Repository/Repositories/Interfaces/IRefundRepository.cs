using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IRefundRepository : IBaseRepository<Refund>
{
    /// <summary>The refund with its payment, cancellation, booking and the user who started it (tracked); null when there is none.</summary>
    Task<Refund?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Every refund of one payment (tracked), so the total that may still be refunded can be worked out.</summary>
    Task<IReadOnlyList<Refund>> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>The refund Stripe knows by this id (re_...), tracked with its payment and booking; null when there is none.</summary>
    Task<Refund?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default);

    /// <summary>Newest first, optionally one status only, without change tracking.</summary>
    Task<IReadOnlyList<Refund>> GetPageAsync(RefundStatus? status, int take, CancellationToken cancellationToken = default);

    /// <summary>Stripe refunds that were requested but never sent (or whose answer was lost): Pending, no Stripe id yet, created before the given time (tracked).</summary>
    Task<IReadOnlyList<Refund>> GetUnsentStripeRefundsAsync(DateTime createdBefore, int take, CancellationToken cancellationToken = default);
}
