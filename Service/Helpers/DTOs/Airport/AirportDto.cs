namespace Service.Helpers.DTOs.Airport;

/// <summary>
/// An airport for the search form's pickers. The last fields come from the destination catalog (airport → city → country) and are empty for an
/// airport that is not in it: the picker shows the country and the names in the three languages without guessing them in the browser.
/// <c>HasRoutes</c> says whether an active route starts or ends here; <c>IsHub</c> marks AEROVA's home base (the default departure).
/// </summary>
public record AirportDto(
    int Id, string Code, string Name, string City, string CountryCode, string TimeZoneId, bool IsActive,
    int? CityId = null, string? CityNameAz = null, string? CityNameRu = null, IReadOnlyList<string>? Aliases = null,
    string? CountryName = null, string? CountryNameAz = null, string? CountryNameRu = null,
    bool IsHub = false, bool HasRoutes = false);

/// <summary>Whether an active route joins two airports (by IATA code).</summary>
public record RouteCheckDto(string Origin, string Destination, bool Exists);
