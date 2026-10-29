using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Promotion e-mail campaigns. Creating or editing a promo code sends nothing. An admin previews the e-mail, confirms it, and the campaign
/// is queued; a background job then sends it to the confirmed, active newsletter subscribers a batch at a time (so no request waits for
/// the mail server). The subscriber is checked again at send time, so someone who unsubscribed after the campaign was queued gets nothing.
/// Every delivery attempt is kept. Only counts are logged: no address, code or text.
/// </summary>
public class CampaignService : ICampaignService
{
    private readonly IBaseRepository<PromoCode> _promoCodes;
    private readonly IEmailCampaignRepository _campaigns;
    private readonly INewsletterSubscriberRepository _subscribers;
    private readonly INewsletterService _newsletter;
    private readonly IAdminAlertService _adminAlerts;
    private readonly IEmailSender _sender;
    private readonly NewsletterSettings _settings;
    private readonly AuthSettings _auth;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        IBaseRepository<PromoCode> promoCodes,
        IEmailCampaignRepository campaigns,
        INewsletterSubscriberRepository subscribers,
        INewsletterService newsletter,
        IAdminAlertService adminAlerts,
        IEmailSender sender,
        IOptions<NewsletterSettings> settings,
        IOptions<AuthSettings> auth,
        TimeProvider timeProvider,
        ILogger<CampaignService> logger)
    {
        _promoCodes = promoCodes;
        _campaigns = campaigns;
        _subscribers = subscribers;
        _newsletter = newsletter;
        _adminAlerts = adminAlerts;
        _sender = sender;
        _settings = settings.Value;
        _auth = auth.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The message an admin sees when the campaign tables are not in the database yet.</summary>
    public const string SetupRequiredMessage = "E-mail campaigns are not set up yet: the latest database migration (AddEmailCampaignsAndAdminAlerts) has not been applied.";

    /// <summary>True when the failure is a table or column that the database does not have (a migration was not applied).</summary>
    public static bool IsMissingTable(Exception exception) => Service.Helpers.DatabaseSetup.IsMissingTable(exception);

    /// <summary>
    /// Runs one admin action. A missing campaign table becomes a clear 422 with the code campaign_setup_required (the page shows what to do);
    /// the full exception is written to the server log, never sent to the page. Any other failure goes on unchanged.
    /// </summary>
    private async Task<T> GuardAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsMissingTable(ex))
        {
            _logger.LogError(ex, "E-mail campaign tables are missing: apply the migration AddEmailCampaignsAndAdminAlerts.");
            throw new BusinessRuleException(SetupRequiredMessage, "campaign_setup_required");
        }
    }

    public Task<CampaignPreviewDto> PreviewAsync(int promoCodeId, string? language, CancellationToken cancellationToken = default)
        => GuardAsync(() => PreviewCoreAsync(promoCodeId, language, cancellationToken));

    public Task<CampaignDto> CreateAsync(int promoCodeId, CreateCampaignRequestDto request, int? adminUserId, CancellationToken cancellationToken = default)
        => GuardAsync(() => CreateCoreAsync(promoCodeId, request, adminUserId, cancellationToken));

    public Task<IReadOnlyList<CampaignDto>> GetRecentAsync(CancellationToken cancellationToken = default)
        => GuardAsync(() => GetRecentCoreAsync(cancellationToken));

    public Task<CampaignDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => GuardAsync(() => GetCoreAsync(id, cancellationToken));

    private async Task<CampaignPreviewDto> PreviewCoreAsync(int promoCodeId, string? language, CancellationToken cancellationToken)
    {
        var promo = await GetPromoAsync(promoCodeId, cancellationToken);
        var recipients = await _subscribers.GetActiveAsync(cancellationToken);
        var existing = await _campaigns.GetByPromoCodeIdAsync(promoCodeId, cancellationToken);
        var problems = Problems(promo, recipients.Count, existing is not null);

        var lang = EmailTemplates.NormalizeLanguage(language);
        var (subject, text, html) = Build(lang, promo.Code, promo.DiscountPercent, promo.ValidFrom, promo.ValidTo, promo.MaxUses,
            $"{_auth.FrontendBaseUrl.TrimEnd('/')}/newsletter-unsubscribe.html?token=(your personal unsubscribe link)");

        return new CampaignPreviewDto(promo.Id, promo.Code, promo.DiscountPercent, promo.ValidFrom, promo.ValidTo, promo.MaxUses,
            recipients.Count, lang, subject, text, html, HashOf(promo), problems.Count == 0, problems);
    }

    private async Task<CampaignDto> CreateCoreAsync(int promoCodeId, CreateCampaignRequestDto request, int? adminUserId, CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            throw new BusinessRuleException("Confirm the campaign to send it.", "campaign_not_confirmed");
        }

        var promo = await GetPromoAsync(promoCodeId, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(request.PreviewHash ?? string.Empty), Encoding.ASCII.GetBytes(HashOf(promo))))
        {
            throw new ConflictException("The promotion changed after the preview. Preview it again before sending.");
        }

        if (await _campaigns.GetByPromoCodeIdAsync(promoCodeId, cancellationToken) is not null)
        {
            throw new ConflictException("A campaign for this promo code was already created.");
        }

        var subscribers = await _subscribers.GetActiveAsync(cancellationToken);
        var problems = Problems(promo, subscribers.Count, false);
        if (problems.Count > 0)
        {
            throw new BusinessRuleException("This promotion cannot be sent: " + string.Join(", ", problems), problems[0]);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var campaign = new EmailCampaign
        {
            PromoCodeId = promo.Id,
            Code = promo.Code,
            DiscountPercent = promo.DiscountPercent,
            ValidFrom = promo.ValidFrom,
            ValidTo = promo.ValidTo,
            MaxUses = promo.MaxUses,
            CreatedByUserId = adminUserId,
            CreatedAt = now
        };
        foreach (var subscriber in subscribers)
        {
            campaign.Recipients.Add(new EmailCampaignRecipient { SubscriberId = subscriber.Id, CreatedAt = now });
        }

        await _campaigns.AddAsync(campaign, cancellationToken);
        try
        {
            await _campaigns.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two confirmations at the same moment: the other one created it.
            throw new ConflictException("A campaign for this promo code was already created.");
        }

        return await ToDtoAsync(campaign, cancellationToken);
    }

    private async Task<IReadOnlyList<CampaignDto>> GetRecentCoreAsync(CancellationToken cancellationToken)
    {
        var list = new List<CampaignDto>();
        foreach (var campaign in await _campaigns.GetRecentAsync(50, cancellationToken))
        {
            list.Add(await ToDtoAsync(campaign, cancellationToken));
        }

        return list;
    }

    private async Task<CampaignDto> GetCoreAsync(int id, CancellationToken cancellationToken)
        => await ToDtoAsync(await _campaigns.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Campaign not found."), cancellationToken);

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_sender.IsConfigured)
        {
            return 0;
        }

        var total = 0;
        foreach (var campaign in await _campaigns.GetOpenAsync(cancellationToken))
        {
            total += await ProcessCampaignAsync(campaign, cancellationToken);
        }

        return total;
    }

    private async Task<int> ProcessCampaignAsync(EmailCampaign campaign, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (campaign.Status == CampaignStatus.Queued)
        {
            campaign.Status = CampaignStatus.Sending;
            campaign.StartedAt = now;
            await _campaigns.SaveChangesAsync(cancellationToken);
        }

        var maxAttempts = Math.Max(1, _settings.CampaignMaxAttempts);
        var batch = await _campaigns.GetPendingBatchAsync(campaign.Id, Math.Max(1, _settings.CampaignBatchSize), cancellationToken);
        int sent = 0, failed = 0, skipped = 0;

        foreach (var recipient in batch)
        {
            // Consent is checked at send time: only a confirmed, active subscriber receives marketing e-mail.
            var subscriber = await _subscribers.GetByIdAsync(recipient.SubscriberId, cancellationToken);
            if (subscriber is null || subscriber.Status != NewsletterStatus.Active)
            {
                recipient.Status = CampaignRecipientStatus.Skipped;
                skipped++;
            }
            else
            {
                var (subject, text, html) = Build(subscriber.Language, campaign.Code, campaign.DiscountPercent, campaign.ValidFrom, campaign.ValidTo,
                    campaign.MaxUses, _newsletter.UnsubscribeLinkFor(subscriber));
                recipient.Attempts++;
                try
                {
                    await _sender.SendAsync(new EmailMessage(subscriber.Email, subject, text, html), cancellationToken);
                    recipient.Status = CampaignRecipientStatus.Sent;
                    recipient.SentAt = _timeProvider.GetUtcNow().UtcDateTime;
                    recipient.LastError = null;
                    sent++;
                }
                catch (EmailDeliveryException)
                {
                    // Tried again on the next run until the allowed attempts are used.
                    recipient.LastError = "delivery_failed";
                    if (recipient.Attempts >= maxAttempts)
                    {
                        recipient.Status = CampaignRecipientStatus.Failed;
                    }

                    failed++;
                }
            }

            recipient.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // One by one: an e-mail that went out is never sent again because a later one failed.
            await _campaigns.SaveChangesAsync(cancellationToken);
        }

        var counts = await _campaigns.GetCountsAsync(campaign.Id, cancellationToken);
        if (counts.GetValueOrDefault(CampaignRecipientStatus.Pending) == 0)
        {
            var failedFinal = counts.GetValueOrDefault(CampaignRecipientStatus.Failed);
            campaign.Status = failedFinal > 0 ? CampaignStatus.CompletedWithErrors : CampaignStatus.Completed;
            campaign.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _adminAlerts.QueueAsync($"campaign:{campaign.Id}:done", "Email campaign finished",
                $"The campaign for promo code {campaign.Code} finished: {counts.GetValueOrDefault(CampaignRecipientStatus.Sent)} sent, " +
                $"{failedFinal} failed, {counts.GetValueOrDefault(CampaignRecipientStatus.Skipped)} skipped (unsubscribed in the meantime).", cancellationToken);
            await _campaigns.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Campaign {CampaignId}: {Sent} sent, {Failed} failed this run, {Skipped} skipped.", campaign.Id, sent, failed, skipped);
        return sent;
    }

    private async Task<PromoCode> GetPromoAsync(int id, CancellationToken cancellationToken)
        => await _promoCodes.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Promo code not found.");

    private List<string> Problems(PromoCode promo, int recipients, bool alreadySent)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var problems = new List<string>();
        if (alreadySent) problems.Add("campaign_already_sent");
        if (!promo.IsActive) problems.Add("promo_inactive");
        if (promo.ValidTo is { } to && to <= now) problems.Add("promo_expired");
        if (promo.MaxUses is { } max && promo.UsedCount >= max) problems.Add("promo_exhausted");
        if (recipients == 0) problems.Add("no_subscribers");
        return problems;
    }

    private (string Subject, string Text, string Html) Build(
        string? language, string code, int percent, DateTime? validFrom, DateTime? validTo, int? maxUses, string unsubscribeLink)
        => CampaignEmailTemplates.Promotion(language, code, percent, validFrom, validTo, maxUses, $"{_auth.FrontendBaseUrl.TrimEnd('/')}/booking.html", unsubscribeLink);

    private static string HashOf(PromoCode promo)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{promo.Id}|{promo.Code}|{promo.DiscountPercent}|{promo.ValidFrom:O}|{promo.ValidTo:O}|{promo.MaxUses}|{promo.IsActive}"))).ToLowerInvariant()[..32];

    private async Task<CampaignDto> ToDtoAsync(EmailCampaign campaign, CancellationToken cancellationToken)
    {
        var counts = await _campaigns.GetCountsAsync(campaign.Id, cancellationToken);
        int Count(CampaignRecipientStatus status) => counts.GetValueOrDefault(status);
        return new CampaignDto(campaign.Id, campaign.PromoCodeId, campaign.Code, campaign.DiscountPercent, campaign.Status, counts.Values.Sum(),
            Count(CampaignRecipientStatus.Sent), Count(CampaignRecipientStatus.Failed), Count(CampaignRecipientStatus.Skipped), Count(CampaignRecipientStatus.Pending),
            campaign.CreatedAt, campaign.StartedAt, campaign.CompletedAt);
    }
}
