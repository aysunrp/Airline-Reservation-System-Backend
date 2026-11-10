using Domain.Enums;

namespace Service.Helpers.DTOs.Newsletter;

/// <summary>What the admin sees before confirming: the real promotion data, the e-mail as it will be sent, and how many people get it.</summary>
public record CampaignPreviewDto(
    int PromoCodeId,
    string Code,
    int DiscountPercent,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int? MaxUses,
    int RecipientCount,
    string Language,
    string Subject,
    string Text,
    string Html,
    /// <summary>Must be sent back to confirm: it proves the admin saw this promotion as it is now.</summary>
    string PreviewHash,
    bool CanSend,
    /// <summary>Why it cannot be sent: promo_inactive, promo_expired, promo_exhausted, no_subscribers, campaign_already_sent.</summary>
    IReadOnlyList<string> Problems);

public class CreateCampaignRequestDto
{
    public string PreviewHash { get; set; } = string.Empty;

    /// <summary>Must be true: the admin chose to send the campaign after previewing it.</summary>
    public bool Confirm { get; set; }
}

public record CampaignDto(
    int Id,
    int? PromoCodeId,
    string Code,
    int DiscountPercent,
    CampaignStatus Status,
    int TotalRecipients,
    int Sent,
    int Failed,
    int Skipped,
    int Pending,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
