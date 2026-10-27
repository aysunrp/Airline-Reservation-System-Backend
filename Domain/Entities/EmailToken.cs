using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A link or code sent by e-mail (verify the address, or reset the password). Only a hash of the token is stored (for a six-digit code
/// an HMAC that needs the server's secret, since a plain hash of six digits could be undone at once); the token itself lives in the
/// e-mail alone. A token works once (<see cref="UsedAt"/>) and only until <see cref="ExpiresAt"/>.
/// </summary>
public class EmailToken : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public EmailTokenPurpose Purpose { get; set; }

    /// <summary>Hex SHA-256 of the token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    /// <summary>Wrong guesses of a reset code. After the allowed number the code is dead, whatever the right answer is.</summary>
    public int Attempts { get; set; }

    /// <summary>The booking a <see cref="EmailTokenPurpose.BookingConfirm"/> link belongs to; empty for the other kinds.</summary>
    public int? BookingId { get; set; }

    /// <summary>What the request carried that the link must remember (for a cancellation: the reason and page language, as JSON).</summary>
    public string? Payload { get; set; }
}
