namespace Service.Helpers.DTOs.Cms;

/// <summary>An uploaded file of the media library. Path is relative to Media/ (e.g. home/img-abc.webp).</summary>
public record MediaFileDto(string Path, string Category, string FileName, string Url, long SizeBytes, DateTime ModifiedUtc, bool IsImage, IReadOnlyList<string> UsedBy);

/// <summary>A picture of the site that has a fixed place on a page (the logo, the About page photos). Path is null while the page uses its built-in picture.</summary>
public record SiteImageSlotDto(string Slot, string Label, string? Path, string? Url);
