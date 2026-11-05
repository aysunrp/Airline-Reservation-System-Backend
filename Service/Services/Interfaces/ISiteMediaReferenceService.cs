using Service.Helpers;

namespace Service.Services.Interfaces;

public interface ISiteMediaReferenceService
{
    Task<bool> IsRelativePathReferencedAsync(string relativePath, SiteMediaReferenceExclusions? exclusions = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file on disk only when no destination, sight, or other reference remains.</summary>
    Task<bool> TryDeleteFileIfUnreferencedAsync(string? relativePath, SiteMediaReferenceExclusions? exclusions = null, CancellationToken cancellationToken = default);
}
