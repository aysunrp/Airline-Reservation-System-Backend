namespace Service.Helpers.DTOs.Airport;

public class AirportRequestDto
{
    /// <summary>Three-letter IATA code, e.g. GYD.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>Two-letter ISO country code, e.g. AZ.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>IANA time zone, e.g. Asia/Baku; schedule times at this airport are read in it.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>Inactive airports are hidden from the search form and cannot be searched.</summary>
    public bool IsActive { get; set; } = true;
}
