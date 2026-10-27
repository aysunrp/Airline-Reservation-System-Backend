using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class StripeWebhookEventRepository : BaseRepository<StripeWebhookEvent>, IStripeWebhookEventRepository
{
    public StripeWebhookEventRepository(AppDbContext context) : base(context)
    {
    }

    public Task<StripeWebhookEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
}
