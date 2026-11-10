using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.DTOs.Seed;

/// <summary>
/// What a demo seed run did, per kind of record (e.g. "Airports"). Records that already existed are counted and
/// left unchanged; records that could not be created are listed with the reason.
/// </summary>
public record DemoSeedResultDto(
    IReadOnlyDictionary<string, int> Added,
    IReadOnlyDictionary<string, int> AlreadyPresent,
    IReadOnlyList<string> Skipped,
    FlightGenerationResultDto? Flights);
