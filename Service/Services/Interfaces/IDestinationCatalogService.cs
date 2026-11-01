using Service.Helpers.DTOs.Destinations;

namespace Service.Services.Interfaces;

public interface IDestinationCatalogService
{
    /// <summary>The public catalog: active countries with their active cities and airports.</summary>
    Task<IReadOnlyList<DestinationCountryDto>> GetPublicAsync(CancellationToken cancellationToken = default);

    /// <summary>One city's destination page by its database id: the city and its airports, its country and the other active cities of that country. NotFound for the home base, a switched-off city or country, or an unknown id.</summary>
    Task<CityDestinationDto> GetCityAsync(int cityId, CancellationToken cancellationToken = default);

    /// <summary>The upcoming flights from the home base to the airports of the city (real flights only; empty when none are scheduled).</summary>
    Task<IReadOnlyList<DestinationFlightDto>> GetCityFlightsAsync(int cityId, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> UpdateCountryAsync(int id, DestinationCountryRequestDto request, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> UpdateCityAsync(int id, DestinationCityRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Everything, switched-off records included (admin).</summary>
    Task<IReadOnlyList<DestinationCountryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> SetCountryActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> SetCityActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IDestinationCatalogSeeder
{
    /// <summary>Adds the catalog from DestinationCatalogData. Idempotent: records are found by country code, city name and airport code and are never overwritten.</summary>
    Task<DestinationSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
}
