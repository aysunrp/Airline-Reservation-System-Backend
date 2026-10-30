using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Adds the destination catalog (countries, cities, airports) when the API starts. It is reference data, so it runs in every environment,
/// but it never applies a migration: while the database is not up to date it only logs what to run. Switch it off with
/// "DestinationCatalog:SeedOnStartup": false. It creates no route, schedule or flight and never changes an existing record.
/// </summary>
public static class DestinationCatalogStartup
{
    public static async Task SeedDestinationCatalogAsync(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<DestinationCatalogSettings>>().Value;
        if (!settings.SeedOnStartup)
        {
            return;
        }

        try
        {
            using var scope = app.Services.CreateScope();
            if (!await DemoSeedStartup.MigrationsAppliedAsync(scope, app.Logger, "The destination catalog was not seeded", CancellationToken.None))
            {
                return;
            }

            var result = await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();
            app.Logger.LogInformation(
                "Destination catalog: {Countries} countries, {Cities} cities and {Airports} airports added, {Linked} existing airports linked, {Present} already present.",
                result.CountriesAdded, result.CitiesAdded, result.AirportsAdded, result.AirportsLinked, result.AlreadyPresent);
        }
        catch (Exception exception)
        {
            // A database that is not reachable (or not created yet) must not stop the API from starting; the catalog is added on the next start.
            app.Logger.LogWarning(exception, "The destination catalog was not seeded.");
        }
    }
}
