using Service.Helpers.DTOs.Flight;

namespace Service.Services.Interfaces;

/// <summary>Admin "Routes" page.</summary>
public interface IAdminRouteService
{
    /// <summary>Optionally filtered by route code or city.</summary>
    Task<IReadOnlyList<RouteDto>> GetAllAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<RouteDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<RouteDto> CreateAsync(RouteRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>The airports of a route can only change while no flight or schedule uses it.</summary>
    Task<RouteDto> UpdateAsync(int id, RouteRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
