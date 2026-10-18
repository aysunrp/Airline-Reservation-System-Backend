using Domain.Entities;
using Service.Helpers.Exceptions;
using Service.Helpers.Scheduling;

namespace Service.Tests;

public class ScheduleTimeCalculatorTests
{
    // Baku and Istanbul keep one offset all year (UTC+4, UTC+3); London and Vienna change for summer time.
    private static readonly Airport Baku = new() { Id = 1, Code = "GYD", City = "Baku", TimeZoneId = "Asia/Baku" };
    private static readonly Airport London = new() { Id = 2, Code = "LHR", City = "London", TimeZoneId = "Europe/London" };
    private static readonly Airport Istanbul = new() { Id = 3, Code = "IST", City = "Istanbul", TimeZoneId = "Europe/Istanbul" };
    private static readonly Airport Vienna = new() { Id = 4, Code = "VIE", City = "Vienna", TimeZoneId = "Europe/Vienna" };

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static FlightSchedule Schedule(Airport from, Airport to, TimeOnly departure, TimeOnly arrival, int dayOffset = 0, params FlightScheduleStop[] stops)
    {
        var schedule = new FlightSchedule
        {
            FlightNumber = "AV 101",
            Route = new Route { Origin = from, OriginAirportId = from.Id, Destination = to, DestinationAirportId = to.Id },
            DepartureTime = departure,
            ArrivalTime = arrival,
            ArrivalDayOffset = dayOffset
        };
        foreach (var stop in stops)
        {
            schedule.Stops.Add(stop);
        }

        return schedule;
    }

    private static FlightScheduleStop Stop(int sequenceNo, Airport airport, int arrivalOffsetMinutes, int layoverMinutes)
        => new() { SequenceNo = sequenceNo, Airport = airport, AirportId = airport.Id, ArrivalOffsetMinutes = arrivalOffsetMinutes, LayoverMinutes = layoverMinutes };

    [Fact]
    public void DirectFlight_UsesEachAirportsOwnTimeZone()
    {
        var times = ScheduleTimeCalculator.Calculate(Schedule(Baku, London, new TimeOnly(9, 30), new TimeOnly(13, 10)), new DateOnly(2026, 11, 10));

        Assert.Equal(Utc(2026, 11, 10, 5, 30), times.DepartureAt);   // Baku UTC+4
        Assert.Equal(Utc(2026, 11, 10, 13, 10), times.ArrivalAt);    // London in winter, UTC+0
        var segment = Assert.Single(times.Segments);
        Assert.Equal((1, Baku.Id, London.Id), (segment.SequenceNo, segment.OriginAirportId, segment.DestinationAirportId));
        Assert.Equal((times.DepartureAt, times.ArrivalAt), (segment.DepartureAt, segment.ArrivalAt));
        Assert.Null(segment.LayoverMinutes);
        Assert.Empty(times.Adjustments);
    }

    [Fact]
    public void SummerTime_ChangesTheUtcArrival()
    {
        var times = ScheduleTimeCalculator.Calculate(Schedule(Baku, London, new TimeOnly(9, 30), new TimeOnly(13, 10)), new DateOnly(2026, 7, 10));

        Assert.Equal(Utc(2026, 7, 10, 5, 30), times.DepartureAt);
        Assert.Equal(Utc(2026, 7, 10, 12, 10), times.ArrivalAt);     // London in summer, UTC+1
    }

    [Fact]
    public void Stopovers_BuildOrderedSegmentsFromOffsetsAndLayovers()
    {
        // Stops are given out of order on purpose.
        var schedule = Schedule(Baku, London, new TimeOnly(7, 15), new TimeOnly(13, 30), 0,
            Stop(2, Vienna, 400, 50),
            Stop(1, Istanbul, 150, 80));

        var times = ScheduleTimeCalculator.Calculate(schedule, new DateOnly(2026, 11, 10));

        Assert.Equal(Utc(2026, 11, 10, 3, 15), times.DepartureAt);
        Assert.Collection(times.Segments,
            s =>
            {
                Assert.Equal((1, Baku.Id, Istanbul.Id, 80), (s.SequenceNo, s.OriginAirportId, s.DestinationAirportId, s.LayoverMinutes));
                Assert.Equal((Utc(2026, 11, 10, 3, 15), Utc(2026, 11, 10, 5, 45)), (s.DepartureAt, s.ArrivalAt));
            },
            s =>
            {
                Assert.Equal((2, Istanbul.Id, Vienna.Id, 50), (s.SequenceNo, s.OriginAirportId, s.DestinationAirportId, s.LayoverMinutes));
                Assert.Equal((Utc(2026, 11, 10, 7, 5), Utc(2026, 11, 10, 9, 55)), (s.DepartureAt, s.ArrivalAt));
            },
            s =>
            {
                Assert.Equal((3, Vienna.Id, London.Id), (s.SequenceNo, s.OriginAirportId, s.DestinationAirportId));
                Assert.Equal((Utc(2026, 11, 10, 10, 45), Utc(2026, 11, 10, 13, 30)), (s.DepartureAt, s.ArrivalAt));
                Assert.Null(s.LayoverMinutes);
            });
    }

    [Fact]
    public void ArrivalDayOffset_LandsOnALaterLocalDay()
    {
        var times = ScheduleTimeCalculator.Calculate(Schedule(London, Baku, new TimeOnly(22, 40), new TimeOnly(7, 35), 1), new DateOnly(2026, 11, 10));

        Assert.Equal(Utc(2026, 11, 10, 22, 40), times.DepartureAt);
        Assert.Equal(Utc(2026, 11, 11, 3, 35), times.ArrivalAt);
        Assert.Equal(new DateOnly(2026, 11, 10), times.ScheduledDate);
    }

    [Fact]
    public void WestboundLocalClockEarlierThanDeparture_IsStillValidInUtc()
    {
        // Istanbul 08:00 (05:00 UTC) to London 07:50 local (07:50 UTC): the local clock goes back, the flight does not.
        var times = ScheduleTimeCalculator.Calculate(Schedule(Istanbul, London, new TimeOnly(8, 0), new TimeOnly(7, 50)), new DateOnly(2026, 11, 10));

        Assert.Equal(TimeSpan.FromMinutes(170), times.ArrivalAt - times.DepartureAt);
    }

    [Fact]
    public void SkippedLocalTime_MovesForwardByTheClockChange()
    {
        // UK clocks jump from 01:00 to 02:00 on 28 Mar 2027, so 01:30 does not exist.
        var adjustments = new List<string>();

        var utc = ScheduleTimeCalculator.ToUtc(new DateOnly(2027, 3, 28), new TimeOnly(1, 30), London, adjustments);

        Assert.Equal(Utc(2027, 3, 28, 1, 30), utc);
        Assert.Equal(new TimeOnly(2, 30), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))));
        Assert.Contains("does not exist", Assert.Single(adjustments));
    }

    [Fact]
    public void RepeatedLocalTime_UsesTheFirstOccurrence()
    {
        // UK clocks go back from 02:00 to 01:00 on 25 Oct 2026, so 01:30 happens twice: first in summer time (UTC+1).
        var adjustments = new List<string>();

        var utc = ScheduleTimeCalculator.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(1, 30), London, adjustments);

        Assert.Equal(Utc(2026, 10, 25, 0, 30), utc);
        Assert.Contains("occurs twice", Assert.Single(adjustments));
    }

    [Fact]
    public void ClockChangeOnTheFlightDay_IsReportedWithTheFlightTimes()
    {
        var times = ScheduleTimeCalculator.Calculate(Schedule(Baku, London, new TimeOnly(3, 0), new TimeOnly(1, 30)), new DateOnly(2027, 3, 28));

        Assert.Equal(Utc(2027, 3, 27, 23, 0), times.DepartureAt);
        Assert.Equal(Utc(2027, 3, 28, 1, 30), times.ArrivalAt);
        Assert.Single(times.Adjustments);
    }

    [Fact]
    public void InconsistentTimes_AreRejected()
    {
        var date = new DateOnly(2026, 11, 10);

        // Lands before it leaves in UTC: Baku 16:45 is 12:45 UTC, London 00:20 the same day is 00:20 UTC.
        Assert.Throws<BusinessRuleException>(() => ScheduleTimeCalculator.Calculate(Schedule(Baku, London, new TimeOnly(16, 45), new TimeOnly(0, 20)), date));

        // Lands in London before it leaves Istanbul (05:45 + 80 min = 07:05 UTC).
        Assert.Throws<BusinessRuleException>(() => ScheduleTimeCalculator.Calculate(
            Schedule(Baku, London, new TimeOnly(7, 15), new TimeOnly(7, 0), 0, Stop(1, Istanbul, 150, 80)), date));

        // A stop reached before the aircraft leaves the previous one.
        Assert.Throws<BusinessRuleException>(() => ScheduleTimeCalculator.Calculate(
            Schedule(Baku, London, new TimeOnly(7, 15), new TimeOnly(13, 30), 0, Stop(1, Istanbul, 150, 80), Stop(2, Vienna, 200, 50)), date));

        // Longer than 24 hours.
        Assert.Throws<BusinessRuleException>(() => ScheduleTimeCalculator.Calculate(Schedule(Baku, London, new TimeOnly(9, 30), new TimeOnly(13, 10), 2), date));
    }

    [Fact]
    public void UnknownTimeZone_IsRejected()
    {
        var nowhere = new Airport { Id = 9, Code = "XXX", TimeZoneId = "Mars/Olympus" };

        var error = Assert.Throws<BusinessRuleException>(() => ScheduleTimeCalculator.ToUtc(new DateOnly(2026, 11, 10), new TimeOnly(9, 0), nowhere));
        Assert.Contains("Mars/Olympus", error.Message);
    }

    [Fact]
    public void LocalDate_IsTheCalendarDayAtTheAirport()
    {
        // 21:30 UTC on 9 Nov is already 01:30 on 10 Nov in Baku.
        Assert.Equal(new DateOnly(2026, 11, 10), ScheduleTimeCalculator.LocalDate(Utc(2026, 11, 9, 21, 30), Baku));
        Assert.Equal(new DateOnly(2026, 11, 9), ScheduleTimeCalculator.LocalDate(Utc(2026, 11, 9, 21, 30), London));
    }
}
