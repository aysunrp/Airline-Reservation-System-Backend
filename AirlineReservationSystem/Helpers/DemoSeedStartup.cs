using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repository.Data;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Seeds the demo data when the API starts, but only in the Development environment and only when
/// "Seed:Enabled" is true. The database must already be migrated: the seed never applies migrations itself.
/// Enable it with, for example:
/// <code>dotnet user-secrets set "Seed:Enabled" "true" --project AirlineReservationSystem</code>
/// or the environment variable <c>Seed__Enabled=true</c>, then start the "http" profile.
/// </summary>
public static class DemoSeedStartup
{
    public static async Task SeedDemoDataIfEnabledAsync(this WebApplication app)
        => await RunAsync(app.Services, app.Environment, app.Logger);

    /// <summary>Returns the seed result, or null when nothing ran (not Development, disabled, or pending migrations).</summary>
    public static async Task<DemoSeedResultDto?> RunAsync(IServiceProvider services, IHostEnvironment environment, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<IOptions<SeedSettings>>().Value;
        if (!settings.Enabled)
        {
            return null;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Seed:Enabled is ignored outside the Development environment ({Environment}).", environment.EnvironmentName);
            return null;
        }

        using var scope = services.CreateScope();
        var pending = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning("Demo data was not seeded: {Count} migration(s) are not applied yet ({Migrations}). Run 'dotnet ef database update' first.",
                pending.Count, string.Join(", ", pending));
            return null;
        }

        logger.LogInformation("Seeding demo data{Flights}.", settings.GenerateFlights ? " and generating its flights (this can take a few minutes)" : string.Empty);
        var result = await scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>().SeedAsync(settings.GenerateFlights, cancellationToken);

        logger.LogInformation("Demo data added: {Added}; already present: {Present}.",
            Describe(result.Added), Describe(result.AlreadyPresent));
        foreach (var skipped in result.Skipped)
        {
            logger.LogWarning("Demo record skipped - {Reason}", skipped);
        }

        if (result.Flights is { } flights)
        {
            logger.LogInformation("Demo flights: {Created} created, {Existing} already existing, {Skipped} skipped, {Conflicts} conflicts.",
                flights.Created.Count, flights.AlreadyExisting, flights.Skipped.Count, flights.Conflicts.Count);
        }

        return result;
    }

    private static string Describe(IReadOnlyDictionary<string, int> counts)
        => counts.Count == 0 ? "none" : string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"));
}
