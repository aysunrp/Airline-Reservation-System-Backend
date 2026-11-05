using Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Media;
using Service.Helpers.DTOs.SiteContent;
using Service.Services.Interfaces;

namespace Service.Services;

public class SightMediaService : ISightMediaService
{
    public const string Page = "sight-media";

    private readonly ISiteContentRepository _contents;
    private readonly ISiteMediaStorage _media;
    private readonly ISiteMediaReferenceService _mediaReferences;

    public SightMediaService(ISiteContentRepository contents, ISiteMediaStorage media, ISiteMediaReferenceService mediaReferences)
    {
        _contents = contents;
        _media = media;
        _mediaReferences = mediaReferences;
    }

    public async Task<IReadOnlyList<SiteContentDto>> GetOverridesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _contents.GetByPageAsync(Page, cancellationToken);
        return rows.Select(c => new SiteContentDto(c.Id, c.Page, c.Key, c.TextAz)).ToList();
    }

    public async Task<MediaUploadResultDto> UploadAsync(string citySlug, int sightIndex, IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateKey(citySlug, sightIndex);
        var key = BuildKey(citySlug, sightIndex);
        var relative = await _media.SaveImageAsync(SiteMediaCategories.Sights, file, cancellationToken: cancellationToken);

        var existing = await _contents.FindTrackedAsync(Page, key, cancellationToken);
        if (existing is null)
        {
            await _contents.AddAsync(new SiteContent { Page = Page, Key = key, TextAz = relative }, cancellationToken);
        }
        else
        {
            var previous = existing.TextAz;
            existing.TextAz = relative;
            _contents.Update(existing);
            if (SiteMediaReferenceService.IsManagedRelativeMediaPath(previous) && !string.Equals(previous, relative, StringComparison.Ordinal))
            {
                await TryDeleteIfUnusedAsync(previous, key, cancellationToken);
            }
        }

        await _contents.SaveChangesAsync(cancellationToken);
        return new MediaUploadResultDto(relative, _media.ToPublicUrl(relative));
    }

    public async Task DeleteAsync(string citySlug, int sightIndex, CancellationToken cancellationToken = default)
    {
        ValidateKey(citySlug, sightIndex);
        var key = BuildKey(citySlug, sightIndex);
        var existing = await _contents.FindTrackedAsync(Page, key, cancellationToken);
        if (existing is null)
        {
            return;
        }

        var previous = existing.TextAz;
        _contents.Delete(existing);
        await _contents.SaveChangesAsync(cancellationToken);

        if (SiteMediaReferenceService.IsManagedRelativeMediaPath(previous))
        {
            await TryDeleteIfUnusedAsync(previous, exceptSightMediaKey: null, cancellationToken);
        }
    }

    private Task TryDeleteIfUnusedAsync(string relativePath, string? exceptSightMediaKey, CancellationToken cancellationToken)
        => _mediaReferences.TryDeleteFileIfUnreferencedAsync(
            relativePath,
            new SiteMediaReferenceExclusions(ExceptSightMediaKey: exceptSightMediaKey),
            cancellationToken);

    private static string BuildKey(string citySlug, int sightIndex) => $"{NormalizeSlug(citySlug)}.{sightIndex}";

    private static void ValidateKey(string citySlug, int sightIndex)
    {
        if (sightIndex < 0 || sightIndex > 20)
        {
            throw new ValidationException("Görünüş indeksi 0–20 aralığında olmalıdır.");
        }

        _ = NormalizeSlug(citySlug);
    }

    private static string NormalizeSlug(string citySlug)
    {
        var slug = (citySlug ?? string.Empty).Trim().ToLowerInvariant();
        if (slug.Length == 0 || slug.Length > 40 || !slug.All(c => char.IsLetterOrDigit(c) || c == '-'))
        {
            throw new ValidationException("Şəhər slug-u etibarsızdır.");
        }

        return slug;
    }
}
