using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Adds <see cref="DestinationCatalogData"/>. A country is found by its code, a city by (country, name) and an airport by its IATA code;
/// what exists is left as it is (an airport already in the database keeps its name and is only linked to its city), so the seed can run
/// any number of times and never overwrites a change made in the admin panel. No route, schedule or flight is ever created here.
/// </summary>
public class DestinationCatalogSeeder : IDestinationCatalogSeeder
{
    private readonly IDestinationCatalogRepository _catalog;

    public DestinationCatalogSeeder(IDestinationCatalogRepository catalog) => _catalog = catalog;

    public async Task<DestinationSeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await _catalog.GetCatalogAsync(cancellationToken)).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var removed = await _catalog.GetRemovedKeysAsync(cancellationToken);
        var codes = DestinationCatalogData.Countries.SelectMany(c => c.Cities).SelectMany(c => c.Airports).Select(a => a.Code).ToList();
        var airports = (await _catalog.GetAirportsByCodesAsync(codes, cancellationToken)).ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        int countriesAdded = 0, citiesAdded = 0, airportsAdded = 0, airportsLinked = 0, present = 0, cityFilled = 0;
        var order = 0;

        foreach (var seed in DestinationCatalogData.Countries)
        {
            order++;
            if (!existing.ContainsKey(seed.Code) && removed.Contains("catalog-removed.country." + seed.Code))
            {
                continue;
            }

            if (!existing.TryGetValue(seed.Code, out var country))
            {
                country = new Country
                {
                    Code = seed.Code, Name = seed.Name, NameAz = seed.NameAz, NameRu = seed.NameRu,
                    Description = seed.Description, DescriptionAz = seed.DescriptionAz, DescriptionRu = seed.DescriptionRu,
                    ImageFile = seed.ImageFile, SortOrder = order
                };
                await _catalog.AddAsync(country, cancellationToken);
                existing[seed.Code] = country;
                countriesAdded++;
            }
            else
            {
                present++;
            }

            var cityOrder = 0;
            foreach (var citySeed in seed.Cities)
            {
                cityOrder++;
                var city = country.Cities.FirstOrDefault(c => string.Equals(c.Name, citySeed.Name, StringComparison.OrdinalIgnoreCase));
                if (city is null && removed.Contains($"catalog-removed.city.{seed.Code}.{citySeed.Name}"))
                {
                    continue;
                }

                if (city is null)
                {
                    city = new DestinationCity
                    {
                        Country = country, Name = citySeed.Name, NameAz = citySeed.NameAz, NameRu = citySeed.NameRu,
                        Aliases = citySeed.Aliases.Length == 0 ? null : string.Join(';', citySeed.Aliases), SortOrder = cityOrder, IsHub = citySeed.IsHub,
                        ImageFile = citySeed.ImageFile, Description = citySeed.Description, DescriptionAz = citySeed.DescriptionAz, DescriptionRu = citySeed.DescriptionRu
                    };
                    await _catalog.AddCityAsync(city, cancellationToken);
                    country.Cities.Add(city);
                    citiesAdded++;
                }
                else
                {
                    // A city that exists keeps what it has; only what it lacks (the photograph and the description, added later) is filled in.
                    var filled = false;
                    if (string.IsNullOrEmpty(city.ImageFile)) { city.ImageFile = citySeed.ImageFile; filled = true; }
                    if (string.IsNullOrEmpty(city.Description)) { city.Description = citySeed.Description; filled = true; }
                    if (string.IsNullOrEmpty(city.DescriptionAz)) { city.DescriptionAz = citySeed.DescriptionAz; filled = true; }
                    if (string.IsNullOrEmpty(city.DescriptionRu)) { city.DescriptionRu = citySeed.DescriptionRu; filled = true; }
                    if (!filled) present++;
                    cityFilled += filled ? 1 : 0;
                }

                foreach (var airportSeed in citySeed.Airports)
                {
                    if (!airports.TryGetValue(airportSeed.Code, out var airport))
                    {
                        airport = new Airport
                        {
                            Code = airportSeed.Code, Name = airportSeed.Name, City = citySeed.Name, CountryCode = seed.Code,
                            TimeZoneId = airportSeed.TimeZoneId, IsActive = true, CityRecord = city
                        };
                        await _catalog.AddAirportAsync(airport, cancellationToken);
                        airports[airportSeed.Code] = airport;
                        airportsAdded++;
                    }
                    else if (airport.CityId is null && airport.CityRecord is null)
                    {
                        // The airport was already there (the demo network or the admin panel): it keeps its own data and joins the catalog.
                        airport.CityRecord = city;
                        airportsLinked++;
                    }
                    else
                    {
                        present++;
                    }
                }
            }
        }

        // Countries that came back (they had been switched off by an earlier, narrower catalog): the country, its catalog cities and airports are on again.
        var reactivated = 0;
        foreach (var code in DestinationCatalogData.ReactivatedCountryCodes)
        {
            var seed = DestinationCatalogData.Countries.FirstOrDefault(c => c.Code == code);
            if (seed is null || !existing.TryGetValue(code, out var back) || back.IsActive)
            {
                continue;
            }

            back.IsActive = true;
            reactivated++;
            foreach (var citySeed in seed.Cities)
            {
                var backCity = back.Cities.FirstOrDefault(c => string.Equals(c.Name, citySeed.Name, StringComparison.OrdinalIgnoreCase));
                if (backCity is not null && !backCity.IsActive) { backCity.IsActive = true; reactivated++; }
                foreach (var airportSeed in citySeed.Airports)
                {
                    if (airports.TryGetValue(airportSeed.Code, out var backAirport) && !backAirport.IsActive) { backAirport.IsActive = true; reactivated++; }
                }
            }
        }

        // What left the catalog is switched off with its cities, airports, routes and schedules. Nothing is deleted: flights, bookings, payments
        // and every relation stay, and an airport that is off is refused by every new search.
        int countriesOff = 0, citiesOff = 0, airportsOff = 0;
        var switchedOff = new List<int>();
        foreach (var code in DestinationCatalogData.RetiredCountryCodes)
        {
            if (!existing.TryGetValue(code, out var retired) || !retired.IsActive)
            {
                continue;
            }

            retired.IsActive = false;
            countriesOff++;
            foreach (var retiredCity in retired.Cities.Where(c => c.IsActive))
            {
                retiredCity.IsActive = false;
                citiesOff++;
            }

            foreach (var retiredAirport in retired.Cities.SelectMany(c => c.Airports).Where(a => a.IsActive))
            {
                retiredAirport.IsActive = false;
                switchedOff.Add(retiredAirport.Id);
                airportsOff++;
            }
        }

        var leaving = (await _catalog.GetAirportsByCodesAsync(DestinationCatalogData.RetiredAirportCodes, cancellationToken)).Where(a => a.IsActive).ToList();
        foreach (var leavingAirport in leaving)
        {
            leavingAirport.IsActive = false;
            switchedOff.Add(leavingAirport.Id);
            airportsOff++;
            var home = leavingAirport.CityRecord;
            if (home is { IsActive: true } && home.Airports.All(a => !a.IsActive))
            {
                home.IsActive = false;
                citiesOff++;
            }
        }

        var (routesOff, schedulesOff) = await _catalog.DeactivateRoutesOfAirportsAsync(switchedOff, cancellationToken);
        var (retiredRoutes, retiredSchedules) = await _catalog.DeactivateRoutesBetweenAsync(DestinationCatalogData.RetiredRoutes, cancellationToken);
        routesOff += retiredRoutes;
        schedulesOff += retiredSchedules;

        if (countriesAdded + citiesAdded + airportsAdded + airportsLinked + cityFilled + countriesOff + citiesOff + airportsOff + reactivated + routesOff + schedulesOff > 0)
        {
            await _catalog.SaveChangesAsync(cancellationToken);
        }

        return new DestinationSeedResultDto(countriesAdded, citiesAdded, airportsAdded, airportsLinked, present, Array.Empty<string>(), countriesOff, citiesOff, airportsOff, routesOff, schedulesOff, reactivated);
    }
}
