namespace Service.Helpers.Settings;

public class AuthSettings
{
    public const string SectionName = "Auth";

    /// <summary>
    /// PBKDF2-SHA256 iterations of new password hashes (OWASP recommends 600,000). The count is stored inside every hash,
    /// so a hash made with fewer iterations keeps working and is upgraded the next time its owner signs in.
    /// </summary>
    public int PasswordHashIterations { get; set; } = 600_000;

    /// <summary>Failed sign-ins for one e-mail address (known or not) before sign-in is paused.</summary>
    public int MaxFailedSignIns { get; set; } = 5;

    /// <summary>How long sign-in stays paused for that e-mail address.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Sign-in and register requests one IP address may make per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 10;
}
