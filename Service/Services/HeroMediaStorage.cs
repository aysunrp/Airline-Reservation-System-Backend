using Microsoft.AspNetCore.Http;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Hero-specific facade over <see cref="ISiteMediaStorage"/> (kept for existing hero video admin).</summary>
public class HeroMediaStorage : IHeroMediaStorage
{
    private readonly ISiteMediaStorage _media;

    public HeroMediaStorage(ISiteMediaStorage media) => _media = media;

    public string NormalizeMediaUrl(string? url, bool required, bool allowVideoExtensions)
        => _media.NormalizeHeroMediaUrl(url, required, allowVideoExtensions);

    public Task<string> SaveVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
        => _media.SaveHeroVideoAsync(file, cancellationToken);

    public Task<string> SavePosterAsync(IFormFile file, CancellationToken cancellationToken = default)
        => _media.SaveHeroPosterAsync(file, cancellationToken);
}
