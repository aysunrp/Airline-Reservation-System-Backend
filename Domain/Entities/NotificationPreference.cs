using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Which optional e-mails an account wants. Booking, payment and security messages are not listed here because they are always sent.
/// An account without a row has every option on, as before this setting existed.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>E-mails about flight changes and check-in reminders.</summary>
    public bool FlightUpdates { get; set; } = true;

    /// <summary>E-mails about support requests and other service messages.</summary>
    public bool SystemMessages { get; set; } = true;

    /// <summary>Campaign and promotion e-mails.</summary>
    public bool Marketing { get; set; } = true;
}
