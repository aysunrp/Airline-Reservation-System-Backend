namespace Service.Helpers.Settings;

/// <summary>Newsletter subscription. Nothing here is secret; the mail server login is the existing Smtp settings.</summary>
public class NewsletterSettings
{
    public const string SectionName = "Newsletter";

    /// <summary>How long the confirmation link works.</summary>
    public int ConfirmationHours { get; set; } = 48;

    /// <summary>A second request for the same address within this time sends no second e-mail.</summary>
    public int ResendCooldownMinutes { get; set; } = 2;

    /// <summary>Subscribe, confirm and unsubscribe requests one IP address may make per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>Recipients an e-mail campaign sends to per run of the background job (about every 15 seconds).</summary>
    public int CampaignBatchSize { get; set; } = 20;

    /// <summary>Delivery attempts for one campaign recipient before it is marked failed.</summary>
    public int CampaignMaxAttempts { get; set; } = 3;
}
