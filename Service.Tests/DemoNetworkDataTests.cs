using Domain.Entities;
using Domain.Enums;
using Service.Helpers.Scheduling;
using Service.Helpers.Seeding;

namespace Service.Tests;

/// <summary>The demonstration network as data: valid airports, consistent clocks, believable timetables, nothing outside the 15 countries.</summary>
public class DemoNetworkDataTests
{
    private static readonly DateOnly First = new(2026, 11, 1);
    private static readonly DateOnly Last = new(2027, 1, 31);

    private static IReadOnlyDictionary<string, Airport> Airports { get; } = BuildAirports();

    private static Dictionary<string, Airport> BuildAirports()
    {
        var map = new Dictionary<string, Airport>();
        var id = 0;
        foreach (var country in DestinationCatalogData.Countries)
        {
            foreach (var airport in country.Cities.SelectMany(c => c.Airports))
            {
                map[airport.Code] = new Airport { Id = ++id, Code = airport.Code, Name = airport.Name, CountryCode = country.Code, TimeZoneId = airport.TimeZoneId };
            }
        }

        return map;
    }

    private static IEnumerable<ScheduleSeed> Network => DemoNetworkData.Schedules;

    [Fact]
    public void EveryRouteAndSchedule_UsesAnAirportOfTheCatalog_AndNeverFliesFromAnAirportToItself()
    {
        Assert.All(DemoNetworkData.Routes, r =>
        {
            Assert.Contains(r.OriginCode, Airports.Keys);
            Assert.Contains(r.DestinationCode, Airports.Keys);
            Assert.NotEqual(r.OriginCode, r.DestinationCode);
            Assert.InRange(r.DistanceKm, 100, 12000);
            Assert.InRange(r.DurationMinutes, 30, 16 * 60);
        });
        Assert.Equal(DemoNetworkData.Routes.Count, DemoNetworkData.Routes.Select(r => (r.OriginCode, r.DestinationCode)).Distinct().Count());
        Assert.All(Network, s => Assert.Contains(DemoNetworkData.Routes, r => r.OriginCode == s.OriginCode && r.DestinationCode == s.DestinationCode));
    }

    [Fact]
    public void EveryRoute_HasItsReturnRoute_WithTheSameDistanceAndBlockTime()
        => Assert.All(DemoNetworkData.Routes, r =>
        {
            var back = Assert.Single(DemoNetworkData.Routes, x => x.OriginCode == r.DestinationCode && x.DestinationCode == r.OriginCode);
            Assert.Equal((r.DistanceKm, r.DurationMinutes), (back.DistanceKm, back.DurationMinutes));
        });

    [Fact]
    public void BakuIsTheHub_AndAzerbaijanAndTheMaldivesStayWithinTheirAirports()
    {
        var fromBaku = DemoNetworkData.Routes.Where(r => r.OriginCode == "GYD").Select(r => r.DestinationCode).ToList();
        Assert.True(fromBaku.Count >= 20);
        Assert.Contains("GNJ", fromBaku);
        Assert.Contains("MLE", fromBaku);
        Assert.DoesNotContain("TBS", fromBaku);
        Assert.Contains("HND", fromBaku);

        var allowed = Airports.Values.Where(a => a.CountryCode == "AZ").Select(a => a.Code).OrderBy(c => c).ToList();
        Assert.Equal(new[] { "GNJ", "GYD", "NAJ" }, allowed);
        Assert.Equal(new[] { "MLE" }, Airports.Values.Where(a => a.CountryCode == "MV").Select(a => a.Code));
        Assert.All(DemoNetworkData.Routes, r => Assert.Contains(Airports[r.OriginCode].CountryCode, DestinationCatalogData.Countries.Select(c => c.Code)));
    }

    [Fact]
    public void EveryScheduleEndsOn31January2027_SoTheWorkerNeverExtendsTheDemoNetwork()
        => Assert.All(Network, s => Assert.Equal(new DateOnly(2027, 1, 31), s.ValidTo));

    [Fact]
    public void FlightNumbers_AreUnique_AndDoNotClashWithTheCoreNetwork()
    {
        var numbers = Network.Select(s => s.FlightNumber).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Empty(numbers.Intersect(DemoSeedData.Schedules.Select(s => s.FlightNumber)));
    }

    [Fact]
    public void EverySchedule_UsesAnAircraftThatExists_AndHasThreeIncreasingFares()
    {
        var registrations = DemoSeedData.Aircraft.Concat(DemoNetworkData.Aircraft).Select(a => a.RegistrationNumber).ToHashSet();
        Assert.Equal(12, registrations.Count);
        Assert.All(DemoNetworkData.Aircraft, a => Assert.Equal((12, 25, 60), (a.BusinessSeats, a.ComfortSeats, a.EconomySeats)));
        Assert.All(Network, s =>
        {
            Assert.Contains(s.AircraftRegistration, registrations);
            Assert.True(s.Economy < s.Comfort && s.Comfort < s.Business, s.FlightNumber);
            Assert.True(s.Economy >= 40, s.FlightNumber);
        });
    }

    [Fact]
    public void EveryOperatingDay_HasAnArrivalAfterTheDeparture_MatchingTheRouteBlockTime_AndTheCorrectNextDay()
    {
        var block = DemoNetworkData.Routes.ToDictionary(r => (r.OriginCode, r.DestinationCode), r => r.DurationMinutes);
        var checkedFlights = 0;
        foreach (var seed in Network)
        {
            var origin = Airports[seed.OriginCode];
            var destination = Airports[seed.DestinationCode];
            var schedule = new FlightSchedule
            {
                FlightNumber = seed.FlightNumber, DepartureTime = seed.Departure, ArrivalTime = seed.Arrival, ArrivalDayOffset = seed.ArrivalDayOffset,
                DaysOfWeek = seed.Days, ValidFrom = DemoSeedData.ScheduleValidFrom, ValidTo = seed.ValidTo,
                Route = new Route { Origin = origin, Destination = destination, OriginAirportId = origin.Id, DestinationAirportId = destination.Id }
            };

            for (var date = First; date <= Last; date = date.AddDays(1))
            {
                var day = date.DayOfWeek == DayOfWeek.Sunday ? ScheduleDays.Sunday : (ScheduleDays)(1 << ((int)date.DayOfWeek - 1));
                if (!seed.Days.HasFlag(day))
                {
                    continue;
                }

                var times = ScheduleTimeCalculator.Calculate(schedule, date);
                Assert.Empty(times.Adjustments);
                Assert.Equal(block[(seed.OriginCode, seed.DestinationCode)], (int)(times.ArrivalAt - times.DepartureAt).TotalMinutes);
                var departureDay = ScheduleTimeCalculator.LocalDate(times.DepartureAt, origin);
                var arrivalDay = ScheduleTimeCalculator.LocalDate(times.ArrivalAt, destination);
                Assert.Equal(seed.ArrivalDayOffset, arrivalDay.DayNumber - departureDay.DayNumber);
                checkedFlights++;
            }
        }

        Assert.InRange(checkedFlights, 1800, 2100);
    }

    [Fact]
    public void Japan_IsNineHoursAheadOfUtc_SoATokyoDepartureAt0800IsTheEveningBeforeUtc()
    {
        var tokyo = Airports["HND"];
        var utc = ScheduleTimeCalculator.ToUtc(new DateOnly(2026, 12, 15), new TimeOnly(8, 0), tokyo);
        Assert.Equal(new DateTime(2026, 12, 14, 23, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void EveryDestinationFliesInNovemberDecemberAndJanuary_OnDifferentDays_AtDifferentTimes()
    {
        foreach (var destination in DemoNetworkData.Routes.Where(r => r.OriginCode == "GYD").Select(r => r.DestinationCode))
        {
            var outbound = Network.Where(s => s.OriginCode == "GYD" && s.DestinationCode == destination).ToList();
            Assert.NotEmpty(outbound);
            foreach (var month in new[] { 11, 12, 1 })
            {
                var year = month == 1 ? 2027 : 2026;
                var days = Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Select(d => new DateOnly(year, month, d));
                Assert.Contains(days, d => outbound.Any(s => s.Days.HasFlag(d.DayOfWeek == DayOfWeek.Sunday ? ScheduleDays.Sunday : (ScheduleDays)(1 << ((int)d.DayOfWeek - 1)))));
            }
        }
    }

    [Fact]
    public void TheTimetable_IsVaried_NotEveryRouteFliesEveryDay_AndNotAtTheSameHour()
    {
        var daily = Network.Count(s => s.Days == ScheduleDays.Everyday);
        Assert.InRange(daily, 1, Network.Count() / 4); // only the busiest routes (Ganja) fly every day
        Assert.True(Network.Select(s => s.Days).Distinct().Count() >= 5);
        Assert.True(Network.Select(s => s.Departure.Hour).Distinct().Count() >= 10);

        var weeklyFlights = Network.GroupBy(s => (s.OriginCode, s.DestinationCode))
            .ToDictionary(g => g.Key, g => g.Sum(s => Enumerable.Range(0, 7).Count(i => s.Days.HasFlag((ScheduleDays)(1 << i)))));
        Assert.InRange(weeklyFlights[("GYD", "GNJ")], 7, 7);
        Assert.InRange(weeklyFlights[("GYD", "FRA")], 3, 5);   // medium demand
        Assert.InRange(weeklyFlights[("GYD", "MLE")], 2, 3);   // low demand
        Assert.InRange(weeklyFlights[("GYD", "HND")], 2, 3);
    }
}
