using Microsoft.AspNetCore.Http;

namespace Service.Services.Interfaces;

public interface IHeroMediaStorage
{
    /// <summary>Validates and normalizes a video or poster URL (https or a safe relative /media/hero path).</summary>
    string NormalizeMediaUrl(string? url, bool required, bool allowVideoExtensions);

    Task<string> SaveVideoAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task<string> SavePosterAsync(IFormFile file, CancellationToken cancellationToken = default);
}
