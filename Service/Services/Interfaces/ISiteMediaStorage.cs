using Microsoft.AspNetCore.Http;

namespace Service.Services.Interfaces;

public interface ISiteMediaStorage
{
    /// <summary>Relative path inside Media/ (e.g. destinations/abc.webp), without a leading slash.</summary>
    string ToRelativePath(string publicOrRelativePath);

    /// <summary>Public URL path (e.g. /media/destinations/abc.webp).</summary>
    string ToPublicUrl(string relativePath);

    Task<string> SaveImageAsync(string category, IFormFile file, long? maxBytes = null, CancellationToken cancellationToken = default);

    Task<string> SaveHeroVideoAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task<string> SaveHeroPosterAsync(IFormFile file, CancellationToken cancellationToken = default);

    string NormalizeHeroMediaUrl(string? url, bool required, bool allowVideoExtensions);

    /// <summary>Deletes a file when it lives under Media/ and passes safety checks. Returns false when nothing was removed.</summary>
    Task<bool> TryDeleteRelativeFileAsync(string? relativePath, CancellationToken cancellationToken = default);
}
