namespace Service.Helpers.Seeding;

/// <summary>Default landing hero (Pexels #35626329, Asad Ansari) shipped under Media/hero on the API host.</summary>
public static class LandingHeroVideoData
{
    public const string Title = "Aerial Night Flight Over City Lights";
    public const string VideoFileName = "aerova-hero-night-flight-4k.mp4";
    public const string MobileVideoFileName = "aerova-hero-night-flight-hd.mp4";
    public const string PosterFileName = "aerova-hero-night-flight-poster.jpg";
    public const string PublicVideoPath = "/media/hero/" + VideoFileName;
    public const string PublicMobileVideoPath = "/media/hero/" + MobileVideoFileName;
    public const string PublicPosterPath = "/media/hero/" + PosterFileName;
    public const string SourceName = "Pexels — Asad Ansari";
    public const string SourceUrl = "https://www.pexels.com/video/aerial-night-flight-over-city-lights-35626329/";
    public const string LicenseInfo = "Pexels License (free for commercial use; attribution appreciated)";
}
