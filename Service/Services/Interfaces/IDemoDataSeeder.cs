using Service.Helpers.DTOs.Seed;

namespace Service.Services.Interfaces;

public interface IDemoDataSeeder
{
    /// <summary>Adds the missing demo records and, when <paramref name="generateFlights"/> is set, generates their flights.</summary>
    Task<DemoSeedResultDto> SeedAsync(bool generateFlights = true, CancellationToken cancellationToken = default);
}
