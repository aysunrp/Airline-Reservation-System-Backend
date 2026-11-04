namespace Service.Helpers.Settings;

public class HeroMediaSettings
{
    public const string SectionName = "HeroMedia";

    /// <summary>Folder under ContentRoot where uploaded hero files are stored.</summary>
    public string StoragePath { get; set; } = "Media/hero";

    /// <summary>URL prefix for serving uploaded files (must match static files middleware).</summary>
    public string PublicPath { get; set; } = "/media/hero";

    public long MaxVideoBytes { get; set; } = 80 * 1024 * 1024;

    public long MaxPosterBytes { get; set; } = 8 * 1024 * 1024;
}
