using Microsoft.AspNetCore.Http;
using Service.Helpers.DTOs.Media;
using Service.Helpers.DTOs.SiteContent;

namespace Service.Services.Interfaces;

public interface ISightMediaService
{
    Task<IReadOnlyList<SiteContentDto>> GetOverridesAsync(CancellationToken cancellationToken = default);

    Task<MediaUploadResultDto> UploadAsync(string citySlug, int sightIndex, IFormFile file, CancellationToken cancellationToken = default);

    Task DeleteAsync(string citySlug, int sightIndex, CancellationToken cancellationToken = default);
}
