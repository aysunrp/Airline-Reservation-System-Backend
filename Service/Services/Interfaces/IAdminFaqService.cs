using Service.Helpers.DTOs.Cms;

namespace Service.Services.Interfaces;

public interface IAdminFaqService
{
    /// <summary>Every question for the admin, in display order.</summary>
    Task<IReadOnlyList<FaqItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The questions shown on the support page (active ones, in display order).</summary>
    Task<IReadOnlyList<FaqItemDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<FaqItemDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<FaqItemDto> CreateAsync(FaqItemRequestDto request, CancellationToken cancellationToken = default);

    Task<FaqItemDto> UpdateAsync(int id, FaqItemRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
