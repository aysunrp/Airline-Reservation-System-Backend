using Service.Helpers.DTOs.Newsletter;

namespace Service.Services.Interfaces;

public interface ICampaignService
{
    /// <summary>The e-mail a promo code would send, built from the saved promotion, with the number of subscribers who would get it.</summary>
    Task<CampaignPreviewDto> PreviewAsync(int promoCodeId, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms and queues the campaign of a promo code. Needs the hash of the preview (so the admin confirmed what is saved now) and
    /// <c>Confirm = true</c>. A promo code has at most one campaign (409 when it already has one). Sending happens in the background.
    /// </summary>
    Task<CampaignDto> CreateAsync(int promoCodeId, CreateCampaignRequestDto request, int? adminUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignDto>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<CampaignDto> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>One run of the background job: sends a batch to each open campaign. Returns the number of e-mails sent.</summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}
