using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IAdminPromoCodeService
{
    Task<IReadOnlyList<AdminPromoCodeDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> CreateAsync(PromoCodeRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminPromoCodeDto> UpdateAsync(int id, PromoCodeRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
