using Domain.Entities;

namespace Repository.Repositories.Interfaces;

/// <summary>The destination catalog: countries with their cities and airports, and what links them to routes.</summary>
public interface IDestinationCatalogRepository : IBaseRepository<Country>
{
    /// <summary>Every country with its cities and their airports, by sort order. Tracked, so the caller can change it.</summary>
    Task<IReadOnlyList<Country>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<Country?> GetCountryAsync(int id, CancellationToken cancellationToken = default);

    Task<DestinationCity?> GetCityAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Airports by IATA code, tracked.</summary>
    Task<IReadOnlyList<Airport>> GetAirportsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);

    Task AddCityAsync(DestinationCity city, CancellationToken cancellationToken = default);

    Task AddAirportAsync(Airport airport, CancellationToken cancellationToken = default);

    /// <summary>The ids of airports that at least one active route starts or ends at.</summary>
    Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>The catalog city with this name in the country (case-insensitive), or null.</summary>
    Task<DestinationCity?> FindCityAsync(string countryCode, string cityName, CancellationToken cancellationToken = default);
}
