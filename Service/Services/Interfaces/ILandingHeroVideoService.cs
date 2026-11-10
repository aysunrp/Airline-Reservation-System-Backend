using Service.Helpers.DTOs.LandingHeroVideo;

namespace Service.Services.Interfaces;

public interface ILandingHeroVideoService
{
    Task<LandingHeroVideoPublicDto?> GetActivePublicAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandingHeroVideoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<LandingHeroVideoDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<LandingHeroVideoDto> CreateAsync(LandingHeroVideoRequestDto request, CancellationToken cancellationToken = default);

    Task<LandingHeroVideoDto> UpdateAsync(int id, LandingHeroVideoRequestDto request, CancellationToken cancellationToken = default);

    Task<LandingHeroVideoDto> SetActiveAsync(int id, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
