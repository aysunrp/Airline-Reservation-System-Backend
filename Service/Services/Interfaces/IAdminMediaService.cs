using Microsoft.AspNetCore.Http;
using Service.Helpers.DTOs.Cms;

namespace Service.Services.Interfaces;

public interface IAdminMediaService
{
    Task<IReadOnlyList<MediaFileDto>> ListAsync(string? category, bool unusedOnly, CancellationToken cancellationToken = default);

    /// <summary>Puts a new image content into an existing image file; every place that shows the file shows the new picture.</summary>
    Task<MediaFileDto> ReplaceAsync(string path, IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file only when nothing shows it.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SiteImageSlotDto>> GetSlotsAsync(CancellationToken cancellationToken = default);

    Task<SiteImageSlotDto> SetSlotAsync(string slot, IFormFile file, CancellationToken cancellationToken = default);

    Task ClearSlotAsync(string slot, CancellationToken cancellationToken = default);
}
