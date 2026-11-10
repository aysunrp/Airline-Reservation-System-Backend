using AirlineReservationSystem.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Data;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>The demo seed against a real database: what it adds, that a second run adds nothing, and that it keeps existing records.</summary>
public class DemoSeedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DemoSeedTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Seed_AddsTheDemoNetworkOnce_AndKeepsExistingRecords()
    {
        // Records an admin made before the seed ran: they must survive unchanged.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Airports.Add(new Airport { Code = "GYD", Name = "My Baku", City = "Baku", CountryCode = "AZ", TimeZoneId = "Asia/Baku" });
            db.PromoCodes.Add(new PromoCode { Code = "AEROVA10", DiscountPercent = 50 });
            await db.SaveChangesAsync();
        }

        var first = await SeedAsync();

        Assert.Empty(first.Skipped);
        Assert.Null(first.Flights);
        Assert.Equal(new Dictionary<string, int>
        {
            ["Airports"] = 7, ["Aircraft"] = 4, ["Routes"] = 10, ["Schedules"] = 27,
            ["BaggageOptions"] = 3, ["MealOptions"] = 3, ["PromoCodes"] = 2, ["CancellationRules"] = 7
        }, first.Added);
        Assert.Equal(new Dictionary<string, int> { ["Airports"] = 1, ["PromoCodes"] = 1 }, first.AlreadyPresent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal("My Baku", (await db.Airports.SingleAsync(a => a.Code == "GYD")).Name);
            Assert.Equal(50, (await db.PromoCodes.SingleAsync(p => p.Code == "AEROVA10")).DiscountPercent);

            var airports = await db.Airports.ToListAsync();
            Assert.Equal(8, airports.Count);
            Assert.All(airports, a => Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById(a.TimeZoneId, out _), a.TimeZoneId));

            var aircraft = await db.Aircrafts.Include(a => a.Seats).ToListAsync();
            Assert.Equal(4, aircraft.Count);
            Assert.All(aircraft, a =>
            {
                Assert.Equal(AircraftStatus.Active, a.Status);
                Assert.Equal((12, 25, 60), (a.Seats.Count(s => s.CabinClass == CabinClass.Business),
                    a.Seats.Count(s => s.CabinClass == CabinClass.Comfort), a.Seats.Count(s => s.CabinClass == CabinClass.Economy)));
                Assert.Equal((1, 20), (a.Seats.Min(s => s.RowNumber), a.Seats.Max(s => s.RowNumber)));
            });

            var schedules = await db.FlightSchedules.Include(s => s.Fares).Include(s => s.Stops).ThenInclude(s => s.Airport)
                .AsSplitQuery().ToListAsync();
            Assert.Equal(27, schedules.Count);
            Assert.All(schedules, s =>
            {
                Assert.Equal(3, s.Fares.Count);
                Assert.NotNull(s.DefaultAircraftId);
                Assert.Equal(ScheduleDays.Everyday, s.DaysOfWeek);
            });
            Assert.Equal(new[] { "TBS", "VIE" }, schedules.Single(s => s.FlightNumber == "AV 612").Stops.OrderBy(s => s.SequenceNo).Select(s => s.Airport.Code));
            Assert.Equal(1, schedules.Single(s => s.FlightNumber == "AV 724").ArrivalDayOffset);
            Assert.Equal(890m, schedules.Single(s => s.FlightNumber == "AV 102").Fares.Single(f => f.CabinClass == CabinClass.Business).BasePrice);

            Assert.Equal(new[] { 35m, 55m, 75m }, await db.BaggageOptions.OrderBy(o => o.Price).Select(o => o.Price).ToListAsync());
            Assert.Equal(new[] { "classic", "wellness", "signature" }, await db.MealOptions.OrderBy(o => o.Price).Select(o => o.Code).ToListAsync());
            Assert.Equal(7, await db.CancellationRules.CountAsync());
        }

        // A second run finds everything and changes nothing.
        var second = await SeedAsync();
        Assert.Empty(second.Added);
        Assert.Empty(second.Skipped);
        Assert.Equal(65, second.AlreadyPresent.Values.Sum()); // 8 + 4 + 10 + 27 + 3 + 3 + 3 + 7

        // The seeded schedules produce flights through the normal generation, once.
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        using (var scope = _factory.Services.CreateScope())
        {
            var generation = scope.ServiceProvider.GetRequiredService<IFlightGenerationService>();
            var flights = await generation.GenerateAsync(null, day, day);
            Assert.Equal(27, flights.Created.Count);
            Assert.Empty(flights.Skipped);
            Assert.Empty((await generation.GenerateAsync(null, day, day)).Created);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var av612 = await db.Flights.Include(f => f.Segments).Include(f => f.Seats).AsSplitQuery().SingleAsync(f => f.FlightNumber == "AV 612");
            Assert.Equal(3, av612.Segments.Count);
            Assert.Equal(97, av612.Seats.Count);
        }
    }

    private async Task<Service.Helpers.DTOs.Seed.DemoSeedResultDto> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>().SeedAsync(generateFlights: false);
    }
}

/// <summary>When the startup hook seeds: Development only, only when enabled, never before the migrations.</summary>
public class DemoSeedStartupTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DemoSeedStartupTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class Environment : IHostEnvironment
    {
        public Environment(string name) => EnvironmentName = name;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "AirlineReservationSystem";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<int> AirportCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Airports.CountAsync();
    }

    [Fact]
    public async Task Startup_SeedsOnlyInDevelopment_AndOnlyWhenEnabled()
    {
        var settings = _factory.Services.GetRequiredService<IOptions<SeedSettings>>().Value;
        var development = new Environment(Environments.Development);
        Assert.False(settings.Enabled);

        // Off by default.
        Assert.Null(await DemoSeedStartup.RunAsync(_factory.Services, development, NullLogger.Instance));
        Assert.Equal(0, await AirportCountAsync());

        settings.Enabled = true;
        settings.GenerateFlights = false;
        try
        {
            // Enabled, but not in Production.
            Assert.Null(await DemoSeedStartup.RunAsync(_factory.Services, new Environment(Environments.Production), NullLogger.Instance));
            Assert.Equal(0, await AirportCountAsync());

            var result = await DemoSeedStartup.RunAsync(_factory.Services, development, NullLogger.Instance);
            Assert.NotNull(result);
            Assert.Equal(8, await AirportCountAsync());
        }
        finally
        {
            settings.Enabled = false;
            settings.GenerateFlights = true;
        }
    }
}
