namespace Service.Helpers.DTOs.Flight;

/// <summary>A row of the admin routes table; Code is e.g. "GYD-LHR".</summary>
public record RouteDto(
    int Id,
    string Code,
    string OriginCode,
    string OriginCity,
    string DestinationCode,
    string DestinationCity,
    int DistanceKm,
    int DurationMinutes,
    bool IsActive);

public class RouteRequestDto
{
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public int DistanceKm { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
