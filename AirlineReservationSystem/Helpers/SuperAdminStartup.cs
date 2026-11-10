using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Creates the first SuperAdmin when "Bootstrap:Enabled" is true and the API starts with no active SuperAdmin. It works in every
/// environment (a production server needs its first SuperAdmin too) but only from server configuration, never from a request:
/// <code>
/// dotnet user-secrets set "Bootstrap:Enabled" "true" --project AirlineReservationSystem
/// dotnet user-secrets set "Bootstrap:SuperAdminEmail" "&lt;address&gt;" --project AirlineReservationSystem
/// dotnet user-secrets set "Bootstrap:SuperAdminPassword" "&lt;12+ characters with letters and digits&gt;" --project AirlineReservationSystem
/// </code>
/// (or the environment variables Bootstrap__Enabled, Bootstrap__SuperAdminEmail, Bootstrap__SuperAdminPassword). It never applies
/// migrations, never changes an existing account and never writes the email or the password to the log. Remove the password from
/// the configuration once the account exists.
/// </summary>
public static class SuperAdminStartup
{
    /// <summary>Returns null when nothing ran (disabled, or migrations still pending).</summary>
    public static async Task<SuperAdminBootstrapResultDto?> RunAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<IOptions<BootstrapSettings>>().Value;
        if (!settings.Enabled)
        {
            return null;
        }

        using var scope = services.CreateScope();
        if (!await DemoSeedStartup.MigrationsAppliedAsync(scope, logger, "The SuperAdmin account was not created", cancellationToken))
        {
            return null;
        }

        SuperAdminBootstrapResultDto result;
        try
        {
            result = await scope.ServiceProvider.GetRequiredService<ISuperAdminProvisioningService>()
                .BootstrapAsync(settings.SuperAdminEmail, settings.SuperAdminPassword, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Two servers starting together can both try; the unique e-mail index lets only one win. Only the kind of failure is logged.
            logger.LogWarning("The SuperAdmin account was not created ({ExceptionType}).", exception.GetType().Name);
            return null;
        }

        switch (result.Outcome)
        {
            case SuperAdminBootstrapOutcome.Created:
                logger.LogInformation("SuperAdmin account created from the Bootstrap settings. Remove Bootstrap:SuperAdminPassword from the configuration now.");
                break;
            case SuperAdminBootstrapOutcome.AlreadyExists:
                logger.LogInformation("An active SuperAdmin already exists; the Bootstrap settings were ignored. You can remove them.");
                break;
            case SuperAdminBootstrapOutcome.EmailTaken:
                logger.LogWarning("Bootstrap:SuperAdminEmail belongs to an existing account; it was not changed or promoted. Use another email, or promote an Admin with the promote-superadmin command.");
                break;
            default:
                logger.LogWarning("The SuperAdmin account was not created: {Problems}", string.Join(" ", result.Problems));
                break;
        }

        return result;
    }
}
