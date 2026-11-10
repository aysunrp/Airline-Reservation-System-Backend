using Service.Helpers.DTOs.LandingHeroVideo;

namespace Service.Services.Interfaces;

public interface ILandingHeroVideoSeeder
{
    /// <summary>Adds the default hero video when the table is empty and Media/hero files are present. Never overwrites admin data.</summary>
    Task<LandingHeroVideoSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
}
