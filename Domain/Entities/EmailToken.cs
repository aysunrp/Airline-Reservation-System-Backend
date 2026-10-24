using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A link sent by e-mail (verify the address, or reset the password). Only the SHA-256 hash of the token is stored; the token itself
/// lives in the e-mail alone. A token works once (<see cref="UsedAt"/>) and only until <see cref="ExpiresAt"/>.
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
}
