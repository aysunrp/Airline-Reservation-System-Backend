using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A promotional e-mail about one promo code, sent to confirmed newsletter subscribers after an admin confirmed it. The promotion is
/// copied into the campaign when it is confirmed, so every recipient gets exactly what the admin previewed. There is at most one campaign
/// per promo code, so a second click, a retried request or another admin cannot send the same promotion twice.
/// </summary>
public class EmailCampaign : BaseEntity
{
    public int? PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }

    public string Code { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxUses { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Queued;
    public int? CreatedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<EmailCampaignRecipient> Recipients { get; set; } = new();
}

public class EmailCampaignRecipient : BaseEntity
{
    public int CampaignId { get; set; }
    public EmailCampaign Campaign { get; set; } = null!;

    public int SubscriberId { get; set; }

    public CampaignRecipientStatus Status { get; set; } = CampaignRecipientStatus.Pending;

    /// <summary>Delivery attempts made so far.</summary>
    public int Attempts { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>A short code for the last failure (never the mail server text or the address).</summary>
    public string? LastError { get; set; }
}
