using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IAdminCancellationRuleService
{
    Task<IReadOnlyList<CancellationRuleDto>> GetAllAsync(CabinClass? cabinClass = null, CancellationToken cancellationToken = default);
    Task<CancellationRuleDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<CancellationRuleDto> CreateAsync(CancellationRuleRequestDto request, CancellationToken cancellationToken = default);
    Task<CancellationRuleDto> UpdateAsync(int id, CancellationRuleRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
