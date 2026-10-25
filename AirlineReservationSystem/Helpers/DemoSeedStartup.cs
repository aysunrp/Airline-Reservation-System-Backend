using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repository.Data;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Development-only startup steps. Both are off by default, are ignored outside the Development environment and
/// never apply migrations themselves (the database must already be up to date).
/// <para>
/// Demo data: set "Seed:Enabled" to true, e.g.
/// <code>dotnet user-secrets set "Seed:Enabled" "true" --project AirlineReservationSystem</code>
/// or the environment variable <c>Seed__Enabled=true</c>, then start the "http" profile.
/// </para>
/// <para>
/// Admin account: keep the credentials out of appsettings and source control.
/// <code>
/// dotnet user-secrets set "Seed:CreateAdmin" "true" --project AirlineReservationSystem
/// dotnet user-secrets set "Seed:AdminEmail" "&lt;your admin email&gt;" --project AirlineReservationSystem
/// dotnet user-secrets set "Seed:AdminPassword" "&lt;8+ characters with letters and digits&gt;" --project AirlineReservationSystem
/// </code>
/// (or <c>Seed__CreateAdmin</c>, <c>Seed__AdminEmail</c>, <c>Seed__AdminPassword</c>). After a start, sign in with
/// POST /api/auth/login using that email and password; the returned token passes the Admin policy.
/// An existing account with the email is never changed or promoted.
/// </para>
/// </summary>
public static class DemoSeedStartup
{
    public static async Task SeedDemoDataIfEnabledAsync(this WebApplication app)
    {
        await CreateAdminAsync(app.Services, app.Environment, app.Logger);
        await RunAsync(app.Services, app.Environment, app.Logger);
    }

    /// <summary>
    /// Creates the development Admin account when "Seed:CreateAdmin" is true in Development. Returns null when the
    /// step did not run. Logs never contain the email or the password.
    /// </summary>
    public static async Task<DevelopmentAdminResultDto?> CreateAdminAsync(IServiceProvider services, IHostEnvironment environment, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<IOptions<SeedSettings>>().Value;
        if (!settings.CreateAdmin)
        {
            return null;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Seed:CreateAdmin is ignored outside the Development environment ({Environment}); no admin account was created.", environment.EnvironmentName);
            return null;
        }

        using var scope = services.CreateScope();
        if (!await MigrationsAppliedAsync(scope, logger, "The development admin account was not created", cancellationToken))
        {
            return null;
        }

        var result = await scope.ServiceProvider.GetRequiredService<IDevelopmentAdminSeeder>()
            .EnsureAdminAsync(settings.AdminEmail, settings.AdminPassword, cancellationToken);

        switch (result.Outcome)
        {
            case DevelopmentAdminOutcome.Created:
                logger.LogInformation("Development admin account created from Seed:AdminEmail.");
                break;
            case DevelopmentAdminOutcome.AlreadyAdmin:
                logger.LogInformation("Development admin account already exists; it was left unchanged.");
                break;
            case DevelopmentAdminOutcome.ExistingNonAdmin:
                logger.LogWarning("Seed:AdminEmail belongs to an existing non-admin account; it was not changed or promoted. Use another email.");
                break;
            default:
                logger.LogWarning("The development admin account was not created: {Problems}", string.Join(" ", result.Problems));
                break;
        }

        return result;
    }

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
        if (!await MigrationsAppliedAsync(scope, logger, "Demo data was not seeded", cancellationToken))
        {
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

    internal static async Task<bool> MigrationsAppliedAsync(IServiceScope scope, ILogger logger, string what, CancellationToken cancellationToken)
    {
        var pending = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            return true;
        }

        logger.LogWarning("{What}: {Count} migration(s) are not applied yet ({Migrations}). Run 'dotnet ef database update' first.",
            what, pending.Count, string.Join(", ", pending));
        return false;
    }

    private static string Describe(IReadOnlyDictionary<string, int> counts)
        => counts.Count == 0 ? "none" : string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"));
}
