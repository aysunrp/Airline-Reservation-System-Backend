using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class AdminAlertRepository : BaseRepository<AdminAlert>, IAdminAlertRepository
{
    public AdminAlertRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<bool> KeyExistsAsync(string eventKey, CancellationToken cancellationToken = default)
        => Set.Local.Any(a => a.EventKey == eventKey) || await Set.AnyAsync(a => a.EventKey == eventKey, cancellationToken);

    public async Task<IReadOnlyList<AdminAlert>> GetDueAsync(int maxAttempts, int take, CancellationToken cancellationToken = default)
        => await Set.Where(a => a.SentAt == null && a.Attempts < maxAttempts).OrderBy(a => a.Id).Take(take).ToListAsync(cancellationToken);
}

public class EmailCampaignRepository : BaseRepository<EmailCampaign>, IEmailCampaignRepository
{
    public EmailCampaignRepository(AppDbContext context) : base(context)
    {
    }

    public Task<EmailCampaign?> GetByPromoCodeIdAsync(int promoCodeId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(c => c.PromoCodeId == promoCodeId, cancellationToken);

    public async Task<IReadOnlyList<EmailCampaign>> GetOpenAsync(CancellationToken cancellationToken = default)
        => await Set.Where(c => c.Status == CampaignStatus.Queued || c.Status == CampaignStatus.Sending).OrderBy(c => c.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EmailCampaign>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderByDescending(c => c.Id).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<CampaignRecipientStatus, int>> GetCountsAsync(int campaignId, CancellationToken cancellationToken = default)
        => await Context.Set<EmailCampaignRecipient>().Where(r => r.CampaignId == campaignId)
            .GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<EmailCampaignRecipient>> GetPendingBatchAsync(int campaignId, int take, CancellationToken cancellationToken = default)
        => await Context.Set<EmailCampaignRecipient>().Where(r => r.CampaignId == campaignId && r.Status == CampaignRecipientStatus.Pending)
            .OrderBy(r => r.Id).Take(take).ToListAsync(cancellationToken);
}
