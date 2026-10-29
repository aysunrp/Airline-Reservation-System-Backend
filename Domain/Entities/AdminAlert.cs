using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A message for the admin e-mail address about one business event (a booking, a payment, a refund...). It is saved together with the event
/// and sent by a background job, so a mail server that is down never loses it and never blocks the event. <see cref="EventKey"/> is unique:
/// the same event (for example a Stripe webhook that is delivered twice) never produces a second message.
/// </summary>
public class AdminAlert : BaseEntity
{
    public string EventKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
}
