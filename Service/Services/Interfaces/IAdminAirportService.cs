using Service.Helpers.DTOs.Airport;

namespace Service.Services.Interfaces;

public interface IAdminAirportService
{
    Task<IReadOnlyList<AirportDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<AirportDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AirportDto> CreateAsync(AirportRequestDto request, CancellationToken cancellationToken = default);
    Task<AirportDto> UpdateAsync(int id, AirportRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
