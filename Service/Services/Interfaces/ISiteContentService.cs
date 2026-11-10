using Service.Helpers.DTOs.SiteContent;

namespace Service.Services.Interfaces;

public interface ISiteContentService
{
    /// <summary>The texts of one page. NotFound when the page has none.</summary>
    Task<IReadOnlyList<SiteContentDto>> GetPageAsync(string page, CancellationToken cancellationToken = default);

    /// <summary>Changes the text of one entry. Validation error for an empty or too long text, NotFound for an unknown id.</summary>
    Task<SiteContentDto> UpdateAsync(int id, SiteContentRequestDto? request, CancellationToken cancellationToken = default);

    Task<SiteContentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SiteContentDto> CreateAsync(SiteContentCreateRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISiteContentSeeder
{
    /// <summary>Adds the texts of SiteContentData whose (page, key) is missing. Idempotent: an existing text is never overwritten.</summary>
    Task<SiteContentSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
}
