using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface INotificationPreferenceRepository : IBaseRepository<NotificationPreference>
{
    /// <summary>The account's preferences (tracked); null when it never saved any (every option is then on).</summary>
    Task<NotificationPreference?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The e-mail addresses (lower case) of the accounts that turned campaign e-mails off.</summary>
    Task<IReadOnlySet<string>> GetEmailsWithoutMarketingAsync(CancellationToken cancellationToken = default);
}
