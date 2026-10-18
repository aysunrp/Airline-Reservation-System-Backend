using Domain.Entities;
using Service.Helpers.DTOs.Airport;

namespace Service.Helpers.Mappings;

public static class AirportMappings
{
    public static AirportDto ToDto(this Airport airport)
        => new(airport.Id, airport.Code, airport.Name, airport.City, airport.CountryCode, airport.TimeZoneId);
}
