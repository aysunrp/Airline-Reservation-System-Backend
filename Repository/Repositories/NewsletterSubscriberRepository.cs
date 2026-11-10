using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class NewsletterSubscriberRepository : BaseRepository<NewsletterSubscriber>, INewsletterSubscriberRepository
{
    public NewsletterSubscriberRepository(AppDbContext context) : base(context)
    {
    }

    public Task<NewsletterSubscriber?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

    public Task<NewsletterSubscriber?> GetByConfirmationHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(s => s.ConfirmationTokenHash == tokenHash, cancellationToken);

    public Task<NewsletterSubscriber?> GetByUnsubscribeKeyAsync(Guid key, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(s => s.UnsubscribeKey == key, cancellationToken);

    public async Task<IReadOnlyList<NewsletterSubscriber>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(s => s.Status == NewsletterStatus.Active).OrderBy(s => s.Id).ToListAsync(cancellationToken);
}
