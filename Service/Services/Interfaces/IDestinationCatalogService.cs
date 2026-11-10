using Microsoft.AspNetCore.Http;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.DTOs.Media;

namespace Service.Services.Interfaces;

public interface IDestinationCatalogService
{
    /// <summary>The public catalog: active countries with their active cities and airports.</summary>
    Task<IReadOnlyList<DestinationCountryDto>> GetPublicAsync(CancellationToken cancellationToken = default);

    /// <summary>One city's destination page by its database id: the city and its airports, its country and the other active cities of that country. NotFound for the home base, a switched-off city or country, or an unknown id.</summary>
    Task<CityDestinationDto> GetCityAsync(int cityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The upcoming flights to the airports of the city (real flights only; empty when none are scheduled). Without <paramref name="fromCode"/> they
    /// are the flights from the home base, or, when the home base has none, the flights from any airport; with it, the flights from that airport.
    /// </summary>
    Task<IReadOnlyList<DestinationFlightDto>> GetCityFlightsAsync(int cityId, string? fromCode = null, CancellationToken cancellationToken = default);

    /// <summary>Adds a country to the catalog (active, last in the order, no photograph yet). 409 when the code exists or the code is one the seed retires.</summary>
    Task<DestinationCountryDto> CreateCountryAsync(DestinationCountryCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>Adds a city with its first airport to a country. 409 for a name or airport that already exists (or an airport that belongs to another city); returns the country.</summary>
    Task<DestinationCountryDto> CreateCityAsync(int countryId, DestinationCityCreateDto request, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> UpdateCountryAsync(int id, DestinationCountryRequestDto request, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> UpdateCityAsync(int id, DestinationCityRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Everything, switched-off records included (admin).</summary>
    Task<IReadOnlyList<DestinationCountryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> SetCountryActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> SetCityActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    Task<MediaUploadResultDto> ReplaceCountryImageAsync(int id, IFormFile file, CancellationToken cancellationToken = default);

    Task<MediaUploadResultDto> ReplaceCityImageAsync(int id, IFormFile file, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> RemoveCountryImageAsync(int id, CancellationToken cancellationToken = default);

    Task<DestinationCountryDto> RemoveCityImageAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a city and its airports. Refused (409) while one of its airports has routes, schedule stops or flights.</summary>
    Task DeleteCityAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a country with its cities and their airports. Refused (409) while any of them is in use.</summary>
    Task DeleteCountryAsync(int id, CancellationToken cancellationToken = default);
}

public interface IDestinationCatalogSeeder
{
    /// <summary>Adds the catalog from DestinationCatalogData. Idempotent: records are found by country code, city name and airport code and are never overwritten.</summary>
    Task<DestinationSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
}
