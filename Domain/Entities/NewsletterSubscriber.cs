using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// An address that asked for AEROVA newsletters. It receives newsletters only while <see cref="Status"/> is Active, which it becomes
/// when its owner opens the confirmation link. Only a hash of the confirmation token is stored; the token lives in the e-mail alone.
/// </summary>
public class NewsletterSubscriber : BaseEntity
{
    /// <summary>Trimmed and lower case; unique.</summary>
    public string Email { get; set; } = string.Empty;

    public NewsletterStatus Status { get; set; } = NewsletterStatus.Pending;

    /// <summary>"az", "en" or "ru": the language of the messages sent to this address.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Hex SHA-256 of the open confirmation token; empty once it is used, expired links are replaced by a new request.</summary>
    public string? ConfirmationTokenHash { get; set; }

    public DateTime? ConfirmationExpiresAt { get; set; }

    /// <summary>When the confirmation e-mail was last handed to the mail server (empty when sending failed); limits repeated e-mails.</summary>
    public DateTime? ConfirmationSentAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? UnsubscribedAt { get; set; }

    /// <summary>
    /// Random public key of the unsubscribe link (not the row id). The link also carries a signature that only the server can make, so a
    /// guessed key is refused before the database is asked.
    /// </summary>
    public Guid UnsubscribeKey { get; set; } = Guid.NewGuid();
}
