using Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.LandingHeroVideo;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class LandingHeroVideoService : ILandingHeroVideoService
{
    private readonly ILandingHeroVideoRepository _videos;
    private readonly IHeroMediaStorage _media;
    private readonly ISiteMediaStorage _siteMedia;
    private readonly IValidator<LandingHeroVideoRequestDto> _validator;

    public LandingHeroVideoService(
        ILandingHeroVideoRepository videos,
        IHeroMediaStorage media,
        ISiteMediaStorage siteMedia,
        IValidator<LandingHeroVideoRequestDto> validator)
    {
        _videos = videos;
        _media = media;
        _siteMedia = siteMedia;
        _validator = validator;
    }

    public async Task<LandingHeroVideoPublicDto?> GetActivePublicAsync(CancellationToken cancellationToken = default)
    {
        var active = await _videos.GetActiveAsync(cancellationToken);
        return active == null ? null : ToPublicDto(active);
    }

    public async Task<IReadOnlyList<LandingHeroVideoDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _videos.GetAllOrderedAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<LandingHeroVideoDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => ToDto(await RequireAsync(id, cancellationToken));

    public async Task<LandingHeroVideoDto> CreateAsync(LandingHeroVideoRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var videoUrl = _media.NormalizeMediaUrl(request.VideoUrl, required: true, allowVideoExtensions: true);
        var posterUrl = NormalizeOptionalUrl(request.PosterUrl, allowVideo: false);
        var mobileUrl = NormalizeOptionalUrl(request.MobileVideoUrl, allowVideo: true);
        ValidatePublishable(videoUrl, request.IsActive == true);

        var entity = new LandingHeroVideo
        {
            Title = RequireTitle(request.Title),
            InternalNotes = TrimOrNull(request.InternalNotes),
            VideoUrl = videoUrl,
            PosterUrl = posterUrl,
            MobileVideoUrl = mobileUrl,
            IsActive = request.IsActive == true,
            SortOrder = request.SortOrder ?? 0,
            SourceName = TrimOrNull(request.SourceName),
            SourceUrl = NormalizeSourceUrl(request.SourceUrl),
            LicenseInfo = TrimOrNull(request.LicenseInfo)
        };

        if (entity.IsActive)
        {
            await DeactivateAllAsync(cancellationToken);
        }

        await _videos.AddAsync(entity, cancellationToken);
        await _videos.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<LandingHeroVideoDto> UpdateAsync(int id, LandingHeroVideoRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await RequireAsync(id, cancellationToken);

        if (request.Title != null)
        {
            entity.Title = RequireTitle(request.Title);
        }

        if (request.InternalNotes != null)
        {
            entity.InternalNotes = TrimOrNull(request.InternalNotes);
        }

        if (request.VideoUrl != null)
        {
            entity.VideoUrl = _media.NormalizeMediaUrl(request.VideoUrl, required: true, allowVideoExtensions: true);
        }

        if (request.PosterUrl != null)
        {
            entity.PosterUrl = NormalizeOptionalUrl(request.PosterUrl, allowVideo: false);
        }

        if (request.MobileVideoUrl != null)
        {
            entity.MobileVideoUrl = NormalizeOptionalUrl(request.MobileVideoUrl, allowVideo: true);
        }

        if (request.SortOrder.HasValue)
        {
            entity.SortOrder = request.SortOrder.Value;
        }

        if (request.SourceName != null)
        {
            entity.SourceName = TrimOrNull(request.SourceName);
        }

        if (request.SourceUrl != null)
        {
            entity.SourceUrl = NormalizeSourceUrl(request.SourceUrl);
        }

        if (request.LicenseInfo != null)
        {
            entity.LicenseInfo = TrimOrNull(request.LicenseInfo);
        }

        var nextActive = request.IsActive ?? entity.IsActive;
        ValidatePublishable(entity.VideoUrl, nextActive);

        if (request.IsActive == true && !entity.IsActive)
        {
            await DeactivateAllAsync(cancellationToken);
            entity.IsActive = true;
        }
        else if (request.IsActive == false)
        {
            entity.IsActive = false;
        }

        _videos.Update(entity);
        await _videos.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<LandingHeroVideoDto> SetActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        ValidatePublishable(entity.VideoUrl, active: true);
        await DeactivateAllAsync(cancellationToken);
        entity.IsActive = true;
        _videos.Update(entity);
        await _videos.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        if (entity.IsActive)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(LandingHeroVideoRequestDto.IsActive), "Yayımda olan hero video silinə bilməz. Əvvəlcə deaktiv edin.")
            });
        }

        var videoUrl = entity.VideoUrl;
        var posterUrl = entity.PosterUrl;
        var mobileUrl = entity.MobileVideoUrl;
        _videos.Delete(entity);
        await _videos.SaveChangesAsync(cancellationToken);

        await TryDeleteManagedFileAsync(videoUrl, cancellationToken);
        await TryDeleteManagedFileAsync(posterUrl, cancellationToken);
        await TryDeleteManagedFileAsync(mobileUrl, cancellationToken);
    }

    private async Task TryDeleteManagedFileAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            var relative = _siteMedia.ToRelativePath(url);
            await _siteMedia.TryDeleteRelativeFileAsync(relative, cancellationToken);
        }
        catch (ValidationException)
        {
            // External CDN URLs are not stored under Media/.
        }
    }

    private async Task DeactivateAllAsync(CancellationToken cancellationToken)
    {
        foreach (var active in await _videos.GetAllActiveTrackedAsync(cancellationToken))
        {
            active.IsActive = false;
            _videos.Update(active);
        }
    }

    private async Task<LandingHeroVideo> RequireAsync(int id, CancellationToken cancellationToken)
        => await _videos.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Hero video tapılmadı.");

    private string? NormalizeOptionalUrl(string? url, bool allowVideo)
    {
        if (url == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return _media.NormalizeMediaUrl(url, required: true, allowVideoExtensions: allowVideo);
    }

    private static void ValidatePublishable(string videoUrl, bool active)
    {
        if (active && string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(LandingHeroVideoRequestDto.VideoUrl), "Aktiv video üçün etibarlı Video URL tələb olunur.")
            });
        }
    }

    private static string RequireTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(LandingHeroVideoRequestDto.Title), "Başlıq tələb olunur.")
            });
        }

        return trimmed;
    }

    private static string? NormalizeSourceUrl(string? url)
    {
        if (url == null)
        {
            return null;
        }

        var trimmed = url.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) || absolute.Scheme is not "https" and not "http")
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(LandingHeroVideoRequestDto.SourceUrl), "Mənbə URL yalnız http və ya https ola bilər.")
            });
        }

        return absolute.ToString();
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static LandingHeroVideoDto ToDto(LandingHeroVideo entity)
        => new(
            entity.Id,
            entity.Title,
            entity.InternalNotes,
            entity.VideoUrl,
            entity.PosterUrl,
            entity.MobileVideoUrl,
            entity.IsActive,
            entity.SortOrder,
            entity.SourceName,
            entity.SourceUrl,
            entity.LicenseInfo,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static LandingHeroVideoPublicDto ToPublicDto(LandingHeroVideo entity)
        => new(entity.VideoUrl, entity.PosterUrl, entity.MobileVideoUrl, entity.UpdatedAt ?? entity.CreatedAt);
}
