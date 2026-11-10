using Domain.Entities;
using Service.Helpers.Exceptions;

namespace Service.Helpers.Scheduling;

/// <summary>One leg of a scheduled flight, in UTC.</summary>
public sealed record ScheduledSegmentTimes(
    int SequenceNo,
    int OriginAirportId,
    int DestinationAirportId,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    int? LayoverMinutes);

/// <summary>
/// The UTC times of a schedule on one operating day. <see cref="Adjustments"/> explains every local time
/// that had to be moved because of a daylight-saving change.
/// </summary>
public sealed record ScheduledFlightTimes(
    DateOnly ScheduledDate,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    IReadOnlyList<ScheduledSegmentTimes> Segments,
    IReadOnlyList<string> Adjustments);

/// <summary>
/// Turns a schedule's local clock times into UTC flight and segment times.
/// The departure time is local to the origin airport on the operating day; the arrival time is local to the
/// destination airport, <c>ArrivalDayOffset</c> days later. Stop times are minutes after departure, so they
/// do not depend on time zones.
/// </summary>
public static class ScheduleTimeCalculator
{
    /// <summary>Same limit as for a single flight.</summary>
    public static readonly TimeSpan MaxFlightDuration = TimeSpan.FromHours(24);

    /// <summary>Expects the route airports and the stop airports to be loaded.</summary>
    public static ScheduledFlightTimes Calculate(FlightSchedule schedule, DateOnly scheduledDate)
    {
        var adjustments = new List<string>();
        var origin = schedule.Route.Origin;
        var destination = schedule.Route.Destination;

        var departureAt = ToUtc(scheduledDate, schedule.DepartureTime, origin, adjustments);
        var arrivalAt = ToUtc(scheduledDate.AddDays(schedule.ArrivalDayOffset), schedule.ArrivalTime, destination, adjustments);

        var segments = new List<ScheduledSegmentTimes>();
        var legOriginId = origin.Id;
        var legDepartureAt = departureAt;

        foreach (var stop in schedule.Stops.OrderBy(s => s.SequenceNo))
        {
            var stopArrivalAt = departureAt.AddMinutes(stop.ArrivalOffsetMinutes);
            if (stopArrivalAt <= legDepartureAt)
            {
                throw new BusinessRuleException($"Stop {stop.Airport.Code} is reached before the aircraft leaves the previous airport.");
            }

            segments.Add(new ScheduledSegmentTimes(segments.Count + 1, legOriginId, stop.AirportId, legDepartureAt, stopArrivalAt, stop.LayoverMinutes));
            legOriginId = stop.AirportId;
            legDepartureAt = stopArrivalAt.AddMinutes(stop.LayoverMinutes);
        }

        if (arrivalAt <= legDepartureAt)
        {
            throw new BusinessRuleException(schedule.Stops.Count == 0
                ? $"On {scheduledDate:dd MMM yyyy} the arrival ({arrivalAt:HH:mm} UTC) is not after the departure ({departureAt:HH:mm} UTC)."
                : $"On {scheduledDate:dd MMM yyyy} the arrival ({arrivalAt:HH:mm} UTC) is not after the departure from the last stop ({legDepartureAt:HH:mm} UTC).");
        }

        if (arrivalAt - departureAt > MaxFlightDuration)
        {
            throw new BusinessRuleException($"On {scheduledDate:dd MMM yyyy} the flight would take longer than {MaxFlightDuration.TotalHours:0} hours.");
        }

        segments.Add(new ScheduledSegmentTimes(segments.Count + 1, legOriginId, destination.Id, legDepartureAt, arrivalAt, null));
        return new ScheduledFlightTimes(scheduledDate, departureAt, arrivalAt, segments, adjustments);
    }

    /// <summary>
    /// Converts a local clock time at an airport to UTC.
    /// A time skipped when clocks go forward is moved forward by the size of the jump (01:30 becomes 02:30);
    /// a time that occurs twice when clocks go back uses its first occurrence (the summer-time one).
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time, Airport airport, ICollection<string>? adjustments = null)
    {
        var zone = FindZone(airport);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // The offset that applied before the jump puts the instant just as far past the change as the clock time was.
            var utc = DateTime.SpecifyKind(local - zone.GetUtcOffset(local.AddDays(-1)), DateTimeKind.Utc);
            adjustments?.Add($"{local:dd MMM yyyy HH:mm} does not exist in {zone.Id} because clocks go forward; " +
                             $"used {TimeZoneInfo.ConvertTimeFromUtc(utc, zone):HH:mm} local time.");
            return utc;
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offset = zone.GetAmbiguousTimeOffsets(local).Max();
            adjustments?.Add($"{local:dd MMM yyyy HH:mm} occurs twice in {zone.Id} because clocks go back; used the first one (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}).");
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>The local calendar date at the airport for a UTC instant.</summary>
    public static DateOnly LocalDate(DateTime utc, Airport airport)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FindZone(airport)));

    /// <summary>The local clock time at the airport for a UTC instant.</summary>
    public static TimeOnly LocalTime(DateTime utc, Airport airport)
        => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FindZone(airport)));

    private static TimeZoneInfo FindZone(Airport airport)
        => TimeZoneInfo.TryFindSystemTimeZoneById(airport.TimeZoneId, out var zone)
            ? zone
            : throw new BusinessRuleException($"Airport {airport.Code} has an unknown time zone '{airport.TimeZoneId}'.");
}
