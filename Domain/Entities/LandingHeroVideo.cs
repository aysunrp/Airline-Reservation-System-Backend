using Domain.Common;

namespace Domain.Entities;

/// <summary>Configurable cinematic background video for the public landing page hero.</summary>
public class LandingHeroVideo : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Internal admin notes (not exposed on the public API).</summary>
    public string? InternalNotes { get; set; }

    /// <summary>Absolute https URL or a path served by the API (for example /media/hero/clip.mp4).</summary>
    public string VideoUrl { get; set; } = string.Empty;

    public string? PosterUrl { get; set; }

    /// <summary>Optional alternate source for narrow viewports.</summary>
    public string? MobileVideoUrl { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public string? SourceName { get; set; }

    public string? SourceUrl { get; set; }

    public string? LicenseInfo { get; set; }
}
