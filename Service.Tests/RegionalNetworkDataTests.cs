using Domain.Entities;
using Domain.Enums;
using Service.Helpers.Scheduling;
using Service.Helpers.Seeding;

namespace Service.Tests;

/// <summary>The regional network as data: direct flights between other countries, consistent clocks and fares, and no aircraft in two places at once.</summary>
public class RegionalNetworkDataTests
{
    private static readonly DateOnly First = new(2026, 11, 9);
    private static readonly DateOnly Last = RegionalNetworkData.ValidTo;

    private static Dictionary<string, Airport> Airports { get; } = DestinationCatalogData.Countries
        .SelectMany(c => c.Cities.SelectMany(city => city.Airports).Select(a => new Airport { Code = a.Code, TimeZoneId = a.TimeZoneId, CountryCode = c.Code }))
        .ToDictionary(a => a.Code);

    private static IEnumerable<ScheduleSeed> Network => RegionalNetworkData.Schedules;

    private static ScheduleDays DayOf(DateOnly date)
        => date.DayOfWeek == DayOfWeek.Sunday ? ScheduleDays.Sunday : (ScheduleDays)(1 << ((int)date.DayOfWeek - 1));

    [Fact]
    public void EveryRoute_UsesAirportsOfTheCatalog_NeverTouchesBaku_AndLinksTwoDifferentCountries()
    {
        Assert.NotEmpty(RegionalNetworkData.Routes);
        Assert.All(RegionalNetworkData.Routes, r =>
        {
            Assert.Contains(r.OriginCode, Airports.Keys);
            Assert.Contains(r.DestinationCode, Airports.Keys);
            Assert.NotEqual("GYD", r.OriginCode);
            Assert.NotEqual("GYD", r.DestinationCode);
            Assert.NotEqual(Airports[r.OriginCode].CountryCode, Airports[r.DestinationCode].CountryCode);
            Assert.InRange(r.DistanceKm, 500, 12000);
        });
        Assert.Equal(RegionalNetworkData.Routes.Count, RegionalNetworkData.Routes.Select(r => (r.OriginCode, r.DestinationCode)).Distinct().Count());
        Assert.All(RegionalNetworkData.Routes, r =>
        {
            var back = Assert.Single(RegionalNetworkData.Routes, x => x.OriginCode == r.DestinationCode && x.DestinationCode == r.OriginCode);
            Assert.Equal((r.DistanceKm, r.DurationMinutes), (back.DistanceKm, back.DurationMinutes));
        });
    }

    [Fact]
    public void TheNetwork_ContainsTheCrossCountryPairsOfTheRequest()
    {
        var pairs = RegionalNetworkData.Routes.Select(r => (r.OriginCode, r.DestinationCode)).ToHashSet();
        foreach (var (a, b) in new[] { ("IST", "MAD"), ("DXB", "FRA"), ("LHR", "JFK"), ("CDG", "MAD"), ("DXB", "CDG"), ("IST", "FCO") })
        {
            Assert.Contains((a, b), pairs);
            Assert.Contains((b, a), pairs);
        }
    }

    [Fact]
    public void FlightNumbers_AreUnique_AndDoNotClashWithTheOtherNetworks_NorWithTheBookedFlights()
    {
        var numbers = Network.Select(s => s.FlightNumber).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Empty(numbers.Intersect(DemoSeedData.Schedules.Concat(DemoNetworkData.Schedules).Select(s => s.FlightNumber)));
        Assert.DoesNotContain("AV 1143", numbers);
        Assert.DoesNotContain("AV 1144", numbers);
        Assert.All(numbers, n => Assert.True(int.Parse(n.Substring(3)) >= 1200, n));
    }

    [Fact]
    public void EveryScheduleEndsOn31January2027_AndEveryRouteHasItsSchedule()
    {
        Assert.All(Network, s => Assert.Equal(new DateOnly(2027, 1, 31), s.ValidTo));
        Assert.All(Network, s => Assert.Contains(RegionalNetworkData.Routes, r => r.OriginCode == s.OriginCode && r.DestinationCode == s.DestinationCode));
        Assert.All(RegionalNetworkData.Routes, r => Assert.Contains(Network, s => s.OriginCode == r.OriginCode && s.DestinationCode == r.DestinationCode));
    }

    [Fact]
    public void TheFares_FollowTheDistance_AndRiseFromEconomyToBusiness()
    {
        Assert.All(Network, s =>
        {
            var route = Assert.Single(RegionalNetworkData.Routes, r => r.OriginCode == s.OriginCode && r.DestinationCode == s.DestinationCode);
            var expected = (decimal)(Math.Round((45 + 0.088 * route.DistanceKm) / 5.0) * 5);
            Assert.Equal(expected, s.Economy);
            Assert.True(s.Economy < s.Comfort && s.Comfort < s.Business, s.FlightNumber);
        });

        decimal Fare(string a, string b) => Network.First(s => s.OriginCode == a && s.DestinationCode == b).Economy;
        Assert.True(Fare("LHR", "JFK") > Fare("LHR", "MAD"));
        Assert.True(Fare("DXB", "JFK") > Fare("DXB", "FRA"));
    }

    [Fact]
    public void EveryOperatingDay_HasTheRouteBlockTime_AndTheArrivalDayFollowsTheTimeZones()
    {
        var block = RegionalNetworkData.Routes.ToDictionary(r => (r.OriginCode, r.DestinationCode), r => r.DurationMinutes);
        var checkedFlights = 0;
        foreach (var seed in Network)
        {
            var origin = Airports[seed.OriginCode];
            var destination = Airports[seed.DestinationCode];
            var schedule = new FlightSchedule
            {
                FlightNumber = seed.FlightNumber, DepartureTime = seed.Departure, ArrivalTime = seed.Arrival, ArrivalDayOffset = seed.ArrivalDayOffset,
                DaysOfWeek = seed.Days, ValidFrom = DemoSeedData.ScheduleValidFrom, ValidTo = seed.ValidTo,
                Route = new Route { Origin = origin, Destination = destination }
            };

            for (var date = First; date <= Last; date = date.AddDays(1))
            {
                if (!seed.Days.HasFlag(DayOf(date)))
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

        Assert.InRange(checkedFlights, 2000, 2600);
    }

    [Fact]
    public void NoAircraft_IsInTwoPlacesAtOnce_AndEveryTurnaroundIsAtLeastNinetyMinutes_AtTheAirportItLanded()
    {
        var flights = new List<(string Aircraft, string Number, string From, string To, DateTime Departure, DateTime Arrival)>();
        foreach (var seed in Network)
        {
            for (var date = First; date <= Last; date = date.AddDays(1))
            {
                if (!seed.Days.HasFlag(DayOf(date)))
                {
                    continue;
                }

                var departure = ScheduleTimeCalculator.ToUtc(date, seed.Departure, Airports[seed.OriginCode]);
                var arrival = ScheduleTimeCalculator.ToUtc(date.AddDays(seed.ArrivalDayOffset), seed.Arrival, Airports[seed.DestinationCode]);
                flights.Add((seed.AircraftRegistration, seed.FlightNumber, seed.OriginCode, seed.DestinationCode, departure, arrival));
            }
        }

        foreach (var byAircraft in flights.GroupBy(f => f.Aircraft))
        {
            var ordered = byAircraft.OrderBy(f => f.Departure).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                var next = ordered[i];
                Assert.True((next.Departure - previous.Arrival).TotalMinutes >= 90, $"{byAircraft.Key}: {previous.Number} -> {next.Number}");
                Assert.Equal(previous.To, next.From);
            }
        }
    }

    [Fact]
    public void TheAircraftOfTheRegionalNetwork_AreNotUsedByTheOtherSeededNetworks_AndTheBookedOneIsLeftAlone()
    {
        var regional = Network.Select(s => s.AircraftRegistration).ToHashSet();
        var others = DemoSeedData.Schedules.Concat(DemoNetworkData.Schedules).Select(s => s.AircraftRegistration).ToHashSet();
        Assert.Empty(regional.Intersect(others));
        Assert.DoesNotContain("4K-AV27", regional);
        Assert.All(RegionalNetworkData.Aircraft, a => Assert.Equal((12, 25, 60), (a.BusinessSeats, a.ComfortSeats, a.EconomySeats)));
    }

    [Fact]
    public void ABakuTraveller_ReachesLondonThroughIstanbul_WithinTheConnectionRules_InEveryMonth()
    {
        // GYD - IST from the core timetable, IST - LHR (IST is a hub of the demonstration network, see DemoSeedData).
        var firstLeg = DemoSeedData.Schedules.Concat(DemoNetworkData.Schedules).Where(s => s.OriginCode == "GYD" && s.DestinationCode == "IST").ToList();
        var secondLeg = DemoSeedData.Schedules.Concat(DemoNetworkData.Schedules).Where(s => s.OriginCode == "IST" && s.DestinationCode == "LHR").ToList();
        Assert.NotEmpty(firstLeg);
        Assert.NotEmpty(secondLeg);

        var months = new HashSet<string>();
        for (var date = First; date <= Last; date = date.AddDays(1))
        {
            foreach (var a in firstLeg.Where(s => s.Days.HasFlag(DayOf(date))))
            {
                var landing = ScheduleTimeCalculator.ToUtc(date.AddDays(a.ArrivalDayOffset), a.Arrival, Airports["IST"]);
                foreach (var b in secondLeg.Where(s => s.Days.HasFlag(DayOf(date.AddDays(a.ArrivalDayOffset)))))
                {
                    var departure = ScheduleTimeCalculator.ToUtc(date.AddDays(a.ArrivalDayOffset), b.Departure, Airports["IST"]);
                    if (ConnectionRules.IsValidTransfer(landing, departure))
                    {
                        months.Add(date.ToString("yyyy-MM"));
                    }
                }
            }
        }

        Assert.Equal(new[] { "2026-11", "2026-12", "2027-01" }, months.OrderBy(m => m));
    }
}
