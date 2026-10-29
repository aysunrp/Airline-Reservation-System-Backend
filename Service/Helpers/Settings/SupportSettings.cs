namespace Service.Helpers.Settings;

/// <summary>Customer support. Nothing here is secret. Contact details are shown to visitors only when they are filled in.</summary>
public class SupportSettings
{
    public const string SectionName = "Support";

    /// <summary>The support e-mail address shown on the Support page. Empty: none is shown (no address is made up).</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Requests and replies one signed-in customer may send per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>The same request or reply sent again within this many seconds is the same one (a retry), not a new one.</summary>
    public int DuplicateWindowSeconds { get; set; } = 120;

    /// <summary>A customer can have at most this many unresolved requests at a time.</summary>
    public int MaxOpenTicketsPerUser { get; set; } = 20;
}
