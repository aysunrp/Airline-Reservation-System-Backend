using Service.Helpers.DTOs.Flight;

namespace Service.Services.Interfaces;

public interface IFlightSearchService
{
    /// <summary>Searches every leg separately (one-way, round trip or multi-city) and returns the flights per leg.</summary>
    Task<IReadOnlyList<FlightSearchLegResultDto>> SearchAsync(FlightSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Price calendar: the cheapest bookable fare for each departure day of one route.</summary>
    Task<IReadOnlyList<DayFareDto>> GetLowestFaresAsync(LowestFaresRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Every bookable flight of one route over a period, cheapest or earliest first (voice search, "cheapest flight" requests).</summary>
    Task<FlightRangeSearchResultDto> SearchRangeAsync(FlightRangeSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Flight details page.</summary>
    Task<FlightResultDto> GetFlightAsync(int flightId, CancellationToken cancellationToken = default);
}
