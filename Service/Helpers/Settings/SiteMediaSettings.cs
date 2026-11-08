namespace Service.Helpers.Settings;

/// <summary>Shared uploaded files under ContentRoot/Media, served at <see cref="PublicPathPrefix"/>.</summary>
public class SiteMediaSettings
{
    public const string SectionName = "SiteMedia";

    public string RootPath { get; set; } = "Media";

    public string PublicPathPrefix { get; set; } = "/media";

    public long MaxImageBytes { get; set; } = 10 * 1024 * 1024;

}
