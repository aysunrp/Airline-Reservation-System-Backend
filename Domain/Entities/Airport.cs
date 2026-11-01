using Domain.Common;

namespace Domain.Entities;

public class Airport : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>IANA time zone of the airport, e.g. "Asia/Baku"; local schedule times are read in this zone.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>The catalog city this airport serves; null for an airport outside the destination catalog.</summary>
    public int? CityId { get; set; }
    public DestinationCity? CityRecord { get; set; }
}
