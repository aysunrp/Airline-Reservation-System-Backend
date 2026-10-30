namespace Service.Helpers.DTOs.Destinations;

/// <summary>An airport of a catalog city. HasRoutes says whether any active route starts or ends here: false means nothing can be searched or booked yet.</summary>
public record DestinationAirportDto(int Id, string Code, string Name, string TimeZoneId, bool IsActive, bool HasRoutes);

public record DestinationCityDto(
    int Id, string Name, string NameAz, string NameRu, IReadOnlyList<string> Aliases, bool IsActive, bool IsHub, IReadOnlyList<DestinationAirportDto> Airports);

public record DestinationCountryDto(
    int Id, string Code, string Name, string NameAz, string NameRu,
    string Description, string DescriptionAz, string DescriptionRu,
    string ImageFile, int SortOrder, bool IsActive, IReadOnlyList<DestinationCityDto> Cities);

/// <summary>What a catalog seed run did; counts only, never record contents.</summary>
public record DestinationSeedResultDto(
    int CountriesAdded, int CitiesAdded, int AirportsAdded, int AirportsLinked, int AlreadyPresent, IReadOnlyList<string> Skipped);
