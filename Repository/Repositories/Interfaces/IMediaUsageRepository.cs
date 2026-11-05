namespace Repository.Repositories.Interfaces;

/// <summary>One place that shows an uploaded file: the path as it is stored and a short label of where it is used.</summary>
public record MediaReference(string Path, string Label);

public interface IMediaUsageRepository
{
    /// <summary>Every stored reference to an uploaded file (home cards, countries, cities, hero videos, site images, sight images).</summary>
    Task<IReadOnlyList<MediaReference>> GetReferencesAsync(CancellationToken cancellationToken = default);
}
