namespace Domain.Enums;

public enum NewsletterStatus
{
    /// <summary>The address was given but its owner has not confirmed it yet. No newsletter is sent.</summary>
    Pending,

    /// <summary>Confirmed by the owner of the address: the only status that receives newsletters.</summary>
    Active,

    /// <summary>The owner opted out. No newsletter is sent until the address is subscribed again and confirmed again.</summary>
    Unsubscribed
}
