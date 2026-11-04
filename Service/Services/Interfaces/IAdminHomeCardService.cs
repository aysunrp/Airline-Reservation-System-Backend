using Microsoft.AspNetCore.Http;
using Service.Helpers.DTOs.Cms;

namespace Service.Services.Interfaces;

public interface IAdminHomeCardService
{
    /// <summary>The cards of a section (every section when null) for the admin, in display order.</summary>
    Task<IReadOnlyList<HomeCardDto>> GetAllAsync(string? section, CancellationToken cancellationToken = default);

    /// <summary>The cards the home page shows (active ones, in display order).</summary>
    Task<IReadOnlyList<HomeCardDto>> GetActiveAsync(string? section, CancellationToken cancellationToken = default);

    Task<HomeCardDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<HomeCardDto> CreateAsync(HomeCardRequestDto request, CancellationToken cancellationToken = default);

    Task<HomeCardDto> UpdateAsync(int id, HomeCardRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the card and, when no other card uses it, the uploaded image file.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Stores the chosen file as the card image and removes the previous uploaded file when nothing else uses it.</summary>
    Task<CardImageDto> UploadImageAsync(int id, IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Clears the card image (and deletes the uploaded file when nothing else uses it).</summary>
    Task RemoveImageAsync(int id, CancellationToken cancellationToken = default);
}
