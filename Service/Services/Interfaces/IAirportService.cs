using Service.Helpers.DTOs.Airport;

namespace Service.Services.Interfaces;

public interface IAirportService
{
    /// <summary>Active airports for the origin/destination pickers on the search form.</summary>
    Task<IReadOnlyList<AirportDto>> GetActiveAsync(CancellationToken cancellationToken = default);
}
