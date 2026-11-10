using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Services.Interfaces;

namespace Service.Services;

public class SiteMediaReferenceService : ISiteMediaReferenceService
{
    private readonly IDestinationCatalogRepository _destinations;
    private readonly ISiteMediaStorage _media;

    public SiteMediaReferenceService(
        IDestinationCatalogRepository destinations,
        ISiteMediaStorage media)
    {
        _destinations = destinations;
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

        return await _destinations.IsImageFileUsedElsewhereAsync(
            normalized,
            exclusions.ExceptCountryId,
            exclusions.ExceptCityId,
            cancellationToken);
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
        return normalized.StartsWith(SiteMediaCategories.Destinations + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
        => (relativePath ?? string.Empty).Trim().Replace('\\', '/');
}
