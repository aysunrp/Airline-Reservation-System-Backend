using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Services.Interfaces;

namespace Service.Services;

public class SiteMediaReferenceService : ISiteMediaReferenceService
{
    public const string SightMediaPage = "sight-media";

    private readonly IDestinationCatalogRepository _destinations;
    private readonly ISiteContentRepository _contents;
    private readonly ISiteMediaStorage _media;

    public SiteMediaReferenceService(
        IDestinationCatalogRepository destinations,
        ISiteContentRepository contents,
        ISiteMediaStorage media)
    {
        _destinations = destinations;
        _contents = contents;
        _media = media;
    }

    public async Task<bool> IsRelativePathReferencedAsync(
        string relativePath,
        SiteMediaReferenceExclusions? exclusions = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (normalized.Length == 0)
        {
            return false;
        }

        exclusions ??= new SiteMediaReferenceExclusions();

        if (await _destinations.IsImageFileUsedElsewhereAsync(
                normalized,
                exclusions.ExceptCountryId,
                exclusions.ExceptCityId,
                cancellationToken))
        {
            return true;
        }

        return await _contents.IsSightMediaPathUsedAsync(normalized, exclusions.ExceptSightMediaKey, cancellationToken);
    }

    public async Task<bool> TryDeleteFileIfUnreferencedAsync(
        string? relativePath,
        SiteMediaReferenceExclusions? exclusions = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsManagedRelativeMediaPath(relativePath))
        {
            return false;
        }

        if (await IsRelativePathReferencedAsync(relativePath!, exclusions, cancellationToken))
        {
            return false;
        }

        return await _media.TryDeleteRelativeFileAsync(relativePath, cancellationToken);
    }

    internal static bool IsManagedRelativeMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim().Replace('\\', '/');
        return normalized.StartsWith(SiteMediaCategories.Destinations + "/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(SiteMediaCategories.Sights + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
        => (relativePath ?? string.Empty).Trim().Replace('\\', '/');
}
