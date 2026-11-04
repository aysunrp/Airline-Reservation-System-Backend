using Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.LandingHeroVideo;
using Service.Helpers.Seeding;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class LandingHeroVideoSeeder : ILandingHeroVideoSeeder
{
    private readonly ILandingHeroVideoRepository _videos;
    private readonly IHostEnvironment _environment;
    private readonly HeroMediaSettings _media;

    public LandingHeroVideoSeeder(
        ILandingHeroVideoRepository videos,
        IHostEnvironment environment,
        IOptions<HeroMediaSettings> media)
    {
        _videos = videos;
        _environment = environment;
        _media = media.Value;
    }

    public async Task<LandingHeroVideoSeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _videos.CountAsync(cancellationToken) > 0)
        {
            return new LandingHeroVideoSeedResultDto(false, true, false);
        }

        var folder = Path.Combine(_environment.ContentRootPath, _media.StoragePath);
        var videoPath = Path.Combine(folder, LandingHeroVideoData.VideoFileName);
        var mobilePath = Path.Combine(folder, LandingHeroVideoData.MobileVideoFileName);
        var posterPath = Path.Combine(folder, LandingHeroVideoData.PosterFileName);
        if (!File.Exists(videoPath) || !File.Exists(posterPath))
        {
            return new LandingHeroVideoSeedResultDto(false, false, true);
        }

        await _videos.AddAsync(new LandingHeroVideo
        {
            Title = LandingHeroVideoData.Title,
            InternalNotes = "Default seed: Pexels night flight over city lights (replaces Pinterest pin with a licensed clip).",
            VideoUrl = LandingHeroVideoData.PublicVideoPath,
            MobileVideoUrl = File.Exists(mobilePath) ? LandingHeroVideoData.PublicMobileVideoPath : null,
            PosterUrl = LandingHeroVideoData.PublicPosterPath,
            IsActive = true,
            SortOrder = 0,
            SourceName = LandingHeroVideoData.SourceName,
            SourceUrl = LandingHeroVideoData.SourceUrl,
            LicenseInfo = LandingHeroVideoData.LicenseInfo
        }, cancellationToken);

        await _videos.SaveChangesAsync(cancellationToken);
        return new LandingHeroVideoSeedResultDto(true, false, false);
    }
}
