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

    /// <summary>Switches off the active routes that start or end at one of the airports, and the active schedules of those routes. Flights and bookings are not touched. Tracked; the caller saves.</summary>
    Task<(int Routes, int Schedules)> DeactivateRoutesOfAirportsAsync(IReadOnlyCollection<int> airportIds, CancellationToken cancellationToken = default);

    /// <summary>Switches off the active routes between the given airport pairs (IATA codes, one direction each) and their active schedules. Flights and bookings are not touched. Tracked; the caller saves.</summary>
    Task<(int Routes, int Schedules)> DeactivateRoutesBetweenAsync(IReadOnlyCollection<(string Origin, string Destination)> pairs, CancellationToken cancellationToken = default);

    /// <summary>Upcoming, not cancelled flights from one airport (or from any airport when it is null) to any of the given airports (one query), earliest first.</summary>
    Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int? originAirportId, IReadOnlyCollection<int> destinationAirportIds, DateTime from, int take, CancellationToken cancellationToken = default);

    /// <summary>The city with its airports (tracked), or null.</summary>
    Task<DestinationCity?> GetCityWithAirportsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The country with its cities and their airports (tracked), or null.</summary>
    Task<Country?> GetCountryWithCitiesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Whether any route, schedule stop or flight segment uses one of the airports (such an airport cannot be deleted).</summary>
    Task<bool> AnyAirportInUseAsync(IReadOnlyCollection<int> airportIds, CancellationToken cancellationToken = default);

    /// <summary>Marks the city and its airports for deletion (the caller saves).</summary>
    void RemoveCity(DestinationCity city);

    /// <summary>Marks the country, its cities and their airports for deletion (the caller saves).</summary>
    void RemoveCountry(Country country);

    /// <summary>Catalog items an admin deleted: the seeder must not add them again.</summary>
    Task<HashSet<string>> GetRemovedKeysAsync(CancellationToken cancellationToken = default);

    Task AddRemovedKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> IsImageFileUsedElsewhereAsync(string imageFile, int? exceptCountryId, int? exceptCityId, CancellationToken cancellationToken = default);
}
