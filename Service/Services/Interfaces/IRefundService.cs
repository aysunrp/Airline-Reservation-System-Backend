using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IRefundService
{
    /// <summary>
    /// Sends a requested Stripe refund to Stripe and records the answer. Does nothing for a refund that is no longer waiting, was
    /// already sent or belongs to the pretend provider. Stripe being unreachable leaves it queued for <see cref="SubmitDueAsync"/>.
    /// </summary>
    Task SubmitAsync(int refundId, CancellationToken cancellationToken = default);

    /// <summary>Sends the Stripe refunds that were requested but never sent (a crash or an outage between cancelling and asking Stripe).</summary>
    Task<int> SubmitDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies what Stripe reports about a refund: its status and, for a failure, the reason. A completed refund is never taken back
    /// and a refund that is already in the reported state is left alone. Stages the change; the caller saves. Returns what happened.
    /// </summary>
    Task<string> ApplyProviderStateAsync(Refund refund, string? providerRefundId, string providerStatus, string? failureReason, CancellationToken cancellationToken = default);

    /// <summary>A SuperAdmin asks again for a refund that Stripe refused or cancelled. It is a new Stripe refund; the old one never paid out.</summary>
    Task<AdminRefundDto> RetryAsync(int refundId, AuditActor actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminRefundDto>> GetAllAsync(RefundStatus? status, int take, CancellationToken cancellationToken = default);
}
