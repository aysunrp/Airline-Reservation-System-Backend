namespace Service.Helpers.DTOs.Flight;

/// <summary>A flight created from a schedule.</summary>
public record GeneratedFlightDto(int FlightId, int ScheduleId, string FlightNumber, DateOnly ScheduledDate, DateTime DepartureAt, DateTime ArrivalAt);

/// <summary>A schedule, or one of its days, that produced no flight, and why.</summary>
public record FlightGenerationSkipDto(int ScheduleId, string FlightNumber, DateOnly? ScheduledDate, string Reason);

public record FlightGenerationResultDto(
    IReadOnlyList<GeneratedFlightDto> Created,
    int AlreadyExisting,
    IReadOnlyList<FlightGenerationSkipDto> Skipped,
    IReadOnlyList<string> TimeAdjustments);
