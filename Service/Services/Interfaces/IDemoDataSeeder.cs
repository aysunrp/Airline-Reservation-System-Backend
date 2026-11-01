using Service.Helpers.DTOs.Seed;

namespace Service.Services.Interfaces;

public interface IDemoDataSeeder
{
    /// <summary>Adds the missing demo records and, when <paramref name="generateFlights"/> is set, generates their flights.</summary>
    Task<DemoSeedResultDto> SeedAsync(bool generateFlights = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the demonstration network (16 countries, their cities and airports, the extra aircraft, routes and schedules for November 2026 to
    /// January 2027) and, when <paramref name="generateFlights"/> is set, the flights of the new schedules only. Safe to run again.
    /// </summary>
    Task<DemoSeedResultDto> SeedNetworkAsync(bool generateFlights = true, CancellationToken cancellationToken = default);
}
