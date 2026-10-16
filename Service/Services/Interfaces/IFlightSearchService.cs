using Service.Helpers.DTOs.Flight;

namespace Service.Services.Interfaces;

public interface IFlightSearchService
{
    /// <summary>Searches every leg separately (one-way, round trip or multi-city) and returns the flights per leg.</summary>
    Task<IReadOnlyList<FlightSearchLegResultDto>> SearchAsync(FlightSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Flight details page.</summary>
    Task<FlightResultDto> GetFlightAsync(int flightId, CancellationToken cancellationToken = default);
}
