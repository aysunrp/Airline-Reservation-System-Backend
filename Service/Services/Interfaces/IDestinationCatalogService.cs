using Service.Helpers.DTOs.Destinations;

namespace Service.Services.Interfaces;

public interface IDestinationCatalogService
{
    /// <summary>The public catalog: active countries with their active cities and airports.</summary>
    Task<IReadOnlyList<DestinationCountryDto>> GetPublicAsync(CancellationToken cancellationToken = default);

    /// <summary>One destination for its details page: an active country (not the hub) with its active cities and airports; NotFound otherwise.</summary>
    Task<DestinationCountryDto> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);

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
