namespace Service.Helpers.DTOs.Flight;

/// <summary>A flight created from a schedule.</summary>
public record GeneratedFlightDto(int FlightId, int ScheduleId, string FlightNumber, DateOnly ScheduledDate, DateTime DepartureAt, DateTime ArrivalAt);

/// <summary>A schedule, or one of its days, that produced no flight, and why.</summary>
public record FlightGenerationSkipDto(int ScheduleId, string FlightNumber, DateOnly? ScheduledDate, string Reason);

/// <summary>A flight another run (or an admin) stored first, between this run's check and its insert.</summary>
public record FlightGenerationConflictDto(int ScheduleId, string FlightNumber, DateOnly ScheduledDate, DateTime DepartureAt);

public record FlightGenerationResultDto(
    IReadOnlyList<GeneratedFlightDto> Created,
    int AlreadyExisting,
    IReadOnlyList<FlightGenerationSkipDto> Skipped,
    IReadOnlyList<FlightGenerationConflictDto> Conflicts,
    IReadOnlyList<string> TimeAdjustments);
