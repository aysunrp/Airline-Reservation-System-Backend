using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

public static class LandingHeroVideoStartup
{
    public static async Task SeedLandingHeroVideoAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            if (!await DemoSeedStartup.MigrationsAppliedAsync(scope, app.Logger, "The landing hero video was not seeded", CancellationToken.None))
            {
                return;
            }

            var result = await scope.ServiceProvider.GetRequiredService<ILandingHeroVideoSeeder>().SeedAsync();
            if (result.Added)
            {
                app.Logger.LogInformation("Landing hero video: default Pexels clip seeded and published.");
            }
            else if (result.MediaMissing)
            {
                app.Logger.LogWarning("Landing hero video: Media/hero files missing; configure a clip in the admin panel.");
            }
            else if (result.AlreadyPresent)
            {
                app.Logger.LogDebug("Landing hero video: already configured; seed skipped.");
            }
        }
        catch (Exception exception)
        {
            app.Logger.LogWarning(exception, "The landing hero video was not seeded.");
        }
    }
}
