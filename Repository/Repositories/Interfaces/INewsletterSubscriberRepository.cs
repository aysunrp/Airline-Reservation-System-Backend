using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface INewsletterSubscriberRepository : IBaseRepository<NewsletterSubscriber>
{
    /// <summary>The subscriber with this normalized address (tracked), or null.</summary>
    Task<NewsletterSubscriber?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>The subscriber whose open confirmation token has this hash (tracked), or null.</summary>
    Task<NewsletterSubscriber?> GetByConfirmationHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<NewsletterSubscriber?> GetByUnsubscribeKeyAsync(Guid key, CancellationToken cancellationToken = default);

    /// <summary>The confirmed, active subscribers: the only people a marketing newsletter may go to.</summary>
    Task<IReadOnlyList<NewsletterSubscriber>> GetActiveAsync(CancellationToken cancellationToken = default);
}
