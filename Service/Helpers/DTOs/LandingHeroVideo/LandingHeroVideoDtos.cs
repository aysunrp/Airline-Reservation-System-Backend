namespace Service.Helpers.DTOs.LandingHeroVideo;

public record LandingHeroVideoPublicDto(string VideoUrl, string? PosterUrl, string? MobileVideoUrl, DateTime? UpdatedAt);

public record LandingHeroVideoDto(
    int Id,
    string Title,
    string? InternalNotes,
    string VideoUrl,
    string? PosterUrl,
    string? MobileVideoUrl,
    bool IsActive,
    int SortOrder,
    string? SourceName,
    string? SourceUrl,
    string? LicenseInfo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class LandingHeroVideoRequestDto
{
    public string? Title { get; set; }
    public string? InternalNotes { get; set; }
    public string? VideoUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? MobileVideoUrl { get; set; }
    public bool? IsActive { get; set; }
    public int? SortOrder { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? LicenseInfo { get; set; }
}
