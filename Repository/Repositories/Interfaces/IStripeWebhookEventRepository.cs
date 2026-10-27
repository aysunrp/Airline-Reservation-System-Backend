using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IStripeWebhookEventRepository : IBaseRepository<StripeWebhookEvent>
{
    /// <summary>The record of this Stripe event (tracked); null when it never arrived before.</summary>
    Task<StripeWebhookEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
}
