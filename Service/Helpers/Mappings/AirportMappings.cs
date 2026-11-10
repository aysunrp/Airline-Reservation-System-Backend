using Domain.Entities;
using Service.Helpers.DTOs.Airport;

namespace Service.Helpers.Mappings;

public static class AirportMappings
{
    public static AirportDto ToDto(this Airport airport)
        => new(airport.Id, airport.Code, airport.Name, airport.City, airport.CountryCode, airport.TimeZoneId, airport.IsActive);

    /// <summary>The picker's version: adds the city and country of the destination catalog and whether the airport has a route.</summary>
    public static AirportDto ToPickerDto(this Airport airport, bool hasRoutes)
    {
        var city = airport.CityRecord;
        var country = city?.Country;
        return new(airport.Id, airport.Code, airport.Name, airport.City, airport.CountryCode, airport.TimeZoneId, airport.IsActive,
            city?.Id, city?.NameAz, city?.NameRu,
            string.IsNullOrWhiteSpace(city?.Aliases) ? null : city!.Aliases!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            country?.Name, country?.NameAz, country?.NameRu, city?.IsHub ?? false, hasRoutes);
    }
}
