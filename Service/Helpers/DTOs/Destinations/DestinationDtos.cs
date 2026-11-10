namespace Service.Helpers.DTOs.Destinations;

/// <summary>An airport of a catalog city. HasRoutes says whether any active route starts or ends here: false means nothing can be searched or booked yet.</summary>
public record DestinationAirportDto(int Id, string Code, string Name, string TimeZoneId, bool IsActive, bool HasRoutes);

public record DestinationCityDto(
    int Id, string Name, string NameAz, string NameRu, IReadOnlyList<string> Aliases, bool IsActive, bool IsHub, IReadOnlyList<DestinationAirportDto> Airports,
    string ImageFile = "", string Description = "", string DescriptionAz = "", string DescriptionRu = "");

/// <summary>The country of a city page: just what the page shows.</summary>
public record DestinationCountrySummaryDto(int Id, string Code, string Name, string NameAz, string NameRu);

/// <summary>
/// One city's destination page: the city with its own airports, its country, and the other active cities of the same country (each with its
/// own photograph, description and airports) for the "explore other cities" collection. The home base (Baku) is not a destination.
/// </summary>
public record CityDestinationDto(DestinationCityDto City, DestinationCountrySummaryDto Country, IReadOnlyList<DestinationCityDto> OtherCities);

/// <summary>An upcoming flight from the home base to an airport of the city. The price is the lowest base fare stored for the flight (null: none stored).</summary>
public record DestinationFlightDto(
    int Id, string FlightNumber, string OriginCode, string OriginName, string DestinationCode, string DestinationName,
    DateTime DepartureAt, DateTime ArrivalAt, string Status, decimal? LowestFare);

public record DestinationCountryDto(
    int Id, string Code, string Name, string NameAz, string NameRu,
    string Description, string DescriptionAz, string DescriptionRu,
    string ImageFile, int SortOrder, bool IsActive, IReadOnlyList<DestinationCityDto> Cities);

/// <summary>An admin edit of a country: its names and the short description in the three languages. The code and the photograph stay as they are.</summary>
public class DestinationCountryRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionAz { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;
}

/// <summary>An admin edit of a city: its Azerbaijani and Russian names and the extra spellings (semicolon separated). The English name is the city's key (airports and searches use it) and stays.</summary>
public class DestinationCityRequestDto
{
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? Aliases { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DescriptionAz { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;
}

/// <summary>A new catalog country: its two-letter ISO code (its stable identity), the names and the short description in the three languages.</summary>
public class DestinationCountryCreateDto : DestinationCountryRequestDto
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// A new catalog city of an existing country with its first airport (a city without an airport cannot be searched or booked). The English name is
/// the city's key. The airport is created, or an existing airport that belongs to no city is linked to it.
/// </summary>
public class DestinationCityCreateDto : DestinationCityRequestDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Three-letter IATA code of the city's airport, e.g. LHR.</summary>
    public string AirportCode { get; set; } = string.Empty;

    public string AirportName { get; set; } = string.Empty;

    /// <summary>IANA time zone of the airport, e.g. Europe/London.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
}

/// <summary>What a catalog seed run did; counts only, never record contents.</summary>
public record DestinationSeedResultDto(
    int CountriesAdded, int CitiesAdded, int AirportsAdded, int AirportsLinked, int AlreadyPresent, IReadOnlyList<string> Skipped,
    int CountriesDeactivated = 0, int CitiesDeactivated = 0, int AirportsDeactivated = 0,
    int RoutesDeactivated = 0, int SchedulesDeactivated = 0, int Reactivated = 0);
