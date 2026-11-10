namespace Service.Helpers.Settings;

/// <summary>
/// The first SuperAdmin. When <see cref="Enabled"/> is true and the API starts with no active SuperAdmin, it creates one from the two
/// values below, once: with a SuperAdmin in place the settings are ignored, and an account that already uses the address is never
/// changed or promoted. All three belong in user-secrets or environment variables (Bootstrap__Enabled and so on), never in appsettings;
/// remove the password again once the account exists.
/// </summary>
public class BootstrapSettings
{
    public const string SectionName = "Bootstrap";

    public bool Enabled { get; set; }

    public string? SuperAdminEmail { get; set; }

    public string? SuperAdminPassword { get; set; }
}
