using Service.Helpers.DTOs.Airport;

namespace Service.Services.Interfaces;

public interface IAirportService
{
    /// <summary>Active airports for the origin/destination pickers on the search form, with their city, country and whether they have a route.</summary>
    Task<IReadOnlyList<AirportDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The active routes as code pairs: the search form offers only airports that are joined (directly or through one transfer).</summary>
    Task<IReadOnlyList<RoutePairDto>> GetNetworkAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether an active route joins the two airports: the search form checks it before a booking is started. Unknown or equal codes are a validation error.</summary>
    Task<RouteCheckDto> CheckRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default);
}
