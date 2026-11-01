using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Adds the texts of the static site pages (SiteContentData) when the API starts. It runs in every environment, but it never applies a
/// migration: while the database is not up to date it only logs what to run. It adds only the texts that are missing and never changes one
/// that exists, so an edit made in the admin panel stays.
/// </summary>
public static class SiteContentStartup
{
    public static async Task SeedSiteContentAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            if (!await DemoSeedStartup.MigrationsAppliedAsync(scope, app.Logger, "The site content was not seeded", CancellationToken.None))
            {
                return;
            }

            var result = await scope.ServiceProvider.GetRequiredService<ISiteContentSeeder>().SeedAsync();
            app.Logger.LogInformation("Site content: {Added} texts added, {Present} already present.", result.Added, result.AlreadyPresent);
        }
        catch (Exception exception)
        {
            // A database that is not reachable (or not created yet) must not stop the API from starting; the texts are added on the next start.
            app.Logger.LogWarning(exception, "The site content was not seeded.");
        }
    }
}
