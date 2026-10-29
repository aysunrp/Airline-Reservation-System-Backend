namespace Domain.Enums;

public enum CampaignStatus
{
    /// <summary>Saved and confirmed by an admin; the background job has not started on it yet.</summary>
    Queued,

    /// <summary>The background job is sending it, a batch at a time.</summary>
    Sending,

    /// <summary>Every recipient was sent or skipped.</summary>
    Completed,

    /// <summary>Finished, but some recipients could not be reached after the allowed attempts.</summary>
    CompletedWithErrors
}

public enum CampaignRecipientStatus
{
    Pending,
    Sent,

    /// <summary>Could not be delivered after the allowed attempts.</summary>
    Failed,

    /// <summary>Left out at send time: the subscriber had unsubscribed (or is no longer an active subscriber) in the meantime.</summary>
    Skipped
}
