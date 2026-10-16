namespace Service.Helpers.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Signing key. Kept in user-secrets (development) or environment variables, never in appsettings.</summary>
    public string Secret { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
