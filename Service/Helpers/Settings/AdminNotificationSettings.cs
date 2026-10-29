namespace Service.Helpers.Settings;

/// <summary>E-mails to the administrator about business events (new booking, payment, cancellation, refund, new subscriber, failures).</summary>
public class AdminNotificationSettings
{
    public const string SectionName = "AdminNotifications";

    public bool Enabled { get; set; } = true;

    /// <summary>Where the messages go. When empty, the sender address of the mail account (Smtp:SenderEmail) is used, so the owner of the account gets them.</summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>A message that cannot be delivered is tried this many times and then left alone.</summary>
    public int MaxAttempts { get; set; } = 5;
}
