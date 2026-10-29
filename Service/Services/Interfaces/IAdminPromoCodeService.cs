using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IAdminPromoCodeService
{
    Task<IReadOnlyList<AdminPromoCodeDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> CreateAsync(PromoCodeRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> UpdateAsync(int id, PromoCodeRequestDto request, CancellationToken cancellationToken = default);
    /// <summary>Deletes a code nobody has used; a code with usage history is deactivated instead and the outcome says which.</summary>
    Task<DeleteOutcome> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
