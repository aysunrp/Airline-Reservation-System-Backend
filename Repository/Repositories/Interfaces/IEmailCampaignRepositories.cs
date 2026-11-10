using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IAdminAlertRepository : IBaseRepository<AdminAlert>
{
    /// <summary>True when an alert with this key exists, saved or waiting to be saved in the current unit of work.</summary>
    Task<bool> KeyExistsAsync(string eventKey, CancellationToken cancellationToken = default);

    /// <summary>Alerts not sent yet with fewer than <paramref name="maxAttempts"/> failures (tracked), oldest first.</summary>
    Task<IReadOnlyList<AdminAlert>> GetDueAsync(int maxAttempts, int take, CancellationToken cancellationToken = default);
}

public interface IEmailCampaignRepository : IBaseRepository<EmailCampaign>
{
    Task<EmailCampaign?> GetByPromoCodeIdAsync(int promoCodeId, CancellationToken cancellationToken = default);

    /// <summary>Campaigns that still have recipients to send to (tracked), oldest first.</summary>
    Task<IReadOnlyList<EmailCampaign>> GetOpenAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailCampaign>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Recipients per status of one campaign.</summary>
    Task<IReadOnlyDictionary<CampaignRecipientStatus, int>> GetCountsAsync(int campaignId, CancellationToken cancellationToken = default);

    /// <summary>The next recipients still waiting (tracked), in a stable order.</summary>
    Task<IReadOnlyList<EmailCampaignRecipient>> GetPendingBatchAsync(int campaignId, int take, CancellationToken cancellationToken = default);
}
