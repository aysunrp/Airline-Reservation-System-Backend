using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>The demonstration network against a real database: what it adds, that a second run adds nothing, and that nothing existing is touched.</summary>
public class DemoNetworkSeedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DemoNetworkSeedTests(ApiFactory factory) => _factory = factory;

    private async Task<Service.Helpers.DTOs.Seed.DemoSeedResultDto> SeedAsync(bool network)
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        return network ? await seeder.SeedNetworkAsync(generateFlights: false) : await seeder.SeedAsync(generateFlights: false);
    }

    [Fact]
    public async Task TheNetwork_AddsWhatIsPlanned_SwitchesOffTheCountriesLeftOut_AndAgainChangesNothing()
    {
        // Records that exist before the network: a country that leaves the catalog (with a city and an airport that have no route), and
        // another one with an active route, which is switched off together with its country.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var qatar = new Country { Code = "QA", Name = "Qatar", NameAz = "Qətər", NameRu = "Катар", SortOrder = 99 };
            var doha = new DestinationCity { Country = qatar, Name = "Doha", NameAz = "Doha", NameRu = "Доха", SortOrder = 1 };
            db.Airports.Add(new Airport { Code = "DOH", Name = "Hamad", City = "Doha", CountryCode = "QA", TimeZoneId = "Asia/Qatar", CityRecord = doha });

            var swiss = new Country { Code = "CH", Name = "Switzerland", NameAz = "İsveçrə", NameRu = "Швейцария", SortOrder = 98 };
            var zurich = new DestinationCity { Country = swiss, Name = "Zurich", NameAz = "Sürix", NameRu = "Цюрих", SortOrder = 1 };
            var zrh = new Airport { Code = "ZRH", Name = "Zurich Airport", City = "Zurich", CountryCode = "CH", TimeZoneId = "Europe/Zurich", CityRecord = zurich };
            db.Airports.Add(zrh);
            await db.SaveChangesAsync();
        }

        var core = await SeedAsync(network: false);
        Assert.Empty(core.Skipped);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gyd = await db.Airports.SingleAsync(a => a.Code == "GYD");
            var zrh = await db.Airports.SingleAsync(a => a.Code == "ZRH");
            db.Routes.Add(new Route { OriginAirportId = gyd.Id, DestinationAirportId = zrh.Id, DistanceKm = 3600, DurationMinutes = 300 });
            await db.SaveChangesAsync();
        }

        var first = await SeedAsync(network: true);

        Assert.Empty(first.Skipped);
        Assert.Equal(15, first.Added["Countries"]);
        Assert.Equal(39, first.Added["Cities"]);
        Assert.Equal(8, first.Added["Aircraft"]);
        Assert.Equal(DemoNetworkData.Routes.Count, first.Added["Routes"]);
        Assert.Equal(DemoNetworkData.Schedules.Count, first.Added["Schedules"]);
        Assert.Equal((2, 2, 3), (first.Added["CountriesSwitchedOff"], first.Added["CitiesSwitchedOff"], first.Added["AirportsSwitchedOff"]));
        Assert.Equal(1, first.Added["RoutesSwitchedOff"]); // 3 airports: Doha, Zurich and Tbilisi (a stop of the core network, not a destination)
        // // the Baku - Zurich route

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.False((await db.Countries.SingleAsync(c => c.Code == "QA")).IsActive);
            Assert.False((await db.DestinationCities.SingleAsync(c => c.Name == "Doha")).IsActive);
            Assert.False((await db.Airports.SingleAsync(a => a.Code == "DOH")).IsActive);
            Assert.False((await db.Countries.SingleAsync(c => c.Code == "CH")).IsActive);
            Assert.False((await db.Airports.SingleAsync(a => a.Code == "ZRH")).IsActive);
            Assert.False((await db.Routes.SingleAsync(r => r.DestinationAirportId == db.Airports.Single(a => a.Code == "ZRH").Id)).IsActive);
            Assert.Equal(1, await db.Airports.CountAsync(a => a.Code == "ZRH")); // nothing was deleted
            Assert.Equal(1, await db.Countries.CountAsync(c => c.Code == "QA")); // nothing was deleted

            Assert.Equal(15, await db.Countries.CountAsync(c => c.IsActive)); // the fifteen of the catalog
            Assert.Equal(4 + 8, await db.Aircrafts.CountAsync());
            Assert.Equal(10 + 1 + DemoNetworkData.Routes.Count, await db.Routes.CountAsync());
            Assert.Equal(27 + DemoNetworkData.Schedules.Count, await db.FlightSchedules.CountAsync());

            var network = DemoNetworkData.Schedules.Select(s => s.FlightNumber).ToList();
            var created = await db.FlightSchedules.Where(s => network.Contains(s.FlightNumber)).Include(s => s.Fares).AsSplitQuery().ToListAsync();
            Assert.Equal(DemoNetworkData.Schedules.Count, created.Count);
            Assert.All(created, s =>
            {
                Assert.Equal(new DateOnly(2027, 1, 31), s.ValidTo);
                Assert.Equal(3, s.Fares.Count);
                Assert.NotNull(s.DefaultAircraftId);
            });
            Assert.Contains(created, s => s.DaysOfWeek != ScheduleDays.Everyday);

            // The core schedules were not touched: still every day, still without an end date.
            Assert.All(await db.FlightSchedules.Where(s => !network.Contains(s.FlightNumber)).ToListAsync(), s =>
            {
                Assert.Null(s.ValidTo);
                Assert.Equal(ScheduleDays.Everyday, s.DaysOfWeek);
            });

            Assert.Equal(new[] { "GNJ", "GYD", "NAJ" }, await db.Airports.Where(a => a.CountryCode == "AZ").OrderBy(a => a.Code).Select(a => a.Code).ToListAsync());
            Assert.Equal(new[] { "MLE" }, await db.Airports.Where(a => a.CountryCode == "MV").Select(a => a.Code).ToListAsync());
        }

        // A second run finds everything and changes nothing.
        var second = await SeedAsync(network: true);
        Assert.Empty(second.Added);
        Assert.Empty(second.Skipped);
    }
}

/// <summary>The flights of the new schedules, made by the normal generator (one day here; the seed makes the three months).</summary>
public class DemoNetworkFlightsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DemoNetworkFlightsTests(ApiFactory factory) => _factory = factory;

    private async Task SeedAsync(bool network)
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        _ = network ? await seeder.SeedNetworkAsync(generateFlights: false) : await seeder.SeedAsync(generateFlights: false);
    }

    [Fact]
    public async Task TheFlightsOfOneDay_AreMadeFromTheNewSchedules_WithSeatsFaresAndTheRightClocks()
    {
        await SeedAsync(network: false);
        await SeedAsync(network: true);

        var day = new DateOnly(2026, 12, 14); // a Monday
        using (var scope = _factory.Services.CreateScope())
        {
            var generation = scope.ServiceProvider.GetRequiredService<IFlightGenerationService>();
            var result = await generation.GenerateAsync(null, day, day);
            Assert.Empty(result.Skipped);
            Assert.Empty(result.Conflicts);
            Assert.Equal(day, result.Created.Min(f => f.ScheduledDate));
            Assert.Empty((await generation.GenerateAsync(null, day, day)).Created); // once only
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var flights = await db.Flights.Where(f => f.ScheduledDate == day).Include(f => f.Route).ThenInclude(r => r.Origin).Include(f => f.Route).ThenInclude(r => r.Destination)
                .Include(f => f.Fares).Include(f => f.Seats).Include(f => f.Segments).Include(f => f.Aircraft).AsSplitQuery().ToListAsync();

            Assert.True(flights.Count >= 35, $"{flights.Count} flights on a Monday");
            Assert.All(flights, f =>
            {
                Assert.Equal(97, f.Seats.Count);
                Assert.Equal(97, f.Seats.Select(s => s.AircraftSeatId).Distinct().Count());
                Assert.All(f.Seats, s => Assert.Equal(SeatStatus.Available, s.Status));
                Assert.Equal(3, f.Fares.Count);
                Assert.True(f.ArrivalAt > f.DepartureAt);
                Assert.Equal(FlightStatus.Scheduled, f.Status);
            });
        }
    }
}
