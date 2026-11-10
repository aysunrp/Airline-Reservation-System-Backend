using Domain.Enums;

namespace Service.Helpers.DTOs.Aircraft;

/// <summary>A row of the admin aircraft table.</summary>
public record AircraftDto(
    int Id,
    string Name,
    string Model,
    string RegistrationNumber,
    AircraftStatus Status,
    int BusinessSeats,
    int ComfortSeats,
    int EconomySeats,
    int TotalSeats);

/// <summary>An aircraft that is on two flights at the same time: how many pairs of flights overlap and the first pair.</summary>
public record AircraftOverlapDto(int AircraftId, string Name, string RegistrationNumber, int Pairs, string FlightA, string FlightB, DateTime At);

public record AircraftSeatDto(int Id, int RowNumber, string Letter, string Label, CabinClass CabinClass);

/// <summary>"View Details": the aircraft with its generated seat layout.</summary>
public record AircraftDetailsDto(AircraftDto Aircraft, IReadOnlyList<AircraftSeatDto> Seats);

public class AircraftRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public AircraftStatus Status { get; set; } = AircraftStatus.Active;
    public int BusinessSeats { get; set; }
    public int ComfortSeats { get; set; }
    public int EconomySeats { get; set; }
}
