using Service.Helpers.DTOs.Aircraft;

namespace Service.Services.Interfaces;

/// <summary>Admin "Aircraft" page.</summary>
public interface IAdminAircraftService
{
    /// <summary>Optionally filtered by name, model or registration number.</summary>
    Task<IReadOnlyList<AircraftDto>> GetAllAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<AircraftDetailsDto> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates the aircraft and generates its seats from the cabin seat counts.</summary>
    Task<AircraftDetailsDto> CreateAsync(AircraftRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Seat counts can only change while no flight or schedule uses the aircraft.</summary>
    Task<AircraftDetailsDto> UpdateAsync(int id, AircraftRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
