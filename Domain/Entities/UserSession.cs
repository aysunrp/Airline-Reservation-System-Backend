using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One sign-in of an account on one device. The token of that sign-in carries <see cref="SessionKey"/> (claim "sid"); a revoked or expired
/// session no longer opens the account, so "sign out of the other devices" really ends them. Only a short description of the device
/// and a masked address are kept, never the token.
/// </summary>
public class UserSession : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Random 32 hex characters, the "sid" claim of the token. Unique.</summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>When the token of this session expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>A short description of the browser and system ("Chrome, Windows"); empty when unknown.</summary>
    public string? Device { get; set; }

    /// <summary>The network address with its last part hidden ("192.168.1.x"); empty when unknown.</summary>
    public string? MaskedIp { get; set; }

    /// <summary>When the session was ended (signed out of, or by "sign out everywhere"); null while it is open.</summary>
    public DateTime? RevokedAt { get; set; }
}
