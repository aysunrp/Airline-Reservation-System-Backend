namespace Domain.Constants;

/// <summary>The sizes of the landing hero video text columns. The database mapping and the request validation both read them from here.</summary>
public static class LandingHeroVideoLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxNotesLength = 2000;
    public const int MaxSourceNameLength = 200;
    public const int MaxLicenseLength = 500;
}

/// <summary>The size of the page text column.</summary>
public static class SiteContentLimits
{
    /// <summary>The longest text a page text may have (the size of the column).</summary>
    public const int MaxTextLength = 1000;
}
