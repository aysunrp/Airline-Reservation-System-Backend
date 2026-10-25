using Service.Helpers.DTOs.Seed;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Commands an operator runs on the server instead of starting the web site. They are not part of the API: nothing a visitor,
/// a User, an Admin or even a SuperAdmin sends over HTTP can reach them.
/// <code>
/// dotnet run --project AirlineReservationSystem -- promote-superadmin --email admin@example.com
/// </code>
/// It makes one existing, active Admin whose e-mail address is confirmed a SuperAdmin, after the operator types the address again.
/// "--yes" skips that question for scripts. Exit code 0 means the account was promoted.
/// </summary>
public static class ServerCommands
{
    public const string PromoteSuperAdmin = "promote-superadmin";

    public static bool IsCommand(string[] args) => args.Length > 0 && string.Equals(args[0], PromoteSuperAdmin, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        var email = ValueOf(args, "--email");
        if (string.IsNullOrWhiteSpace(email))
        {
            await output.WriteLineAsync($"Usage: {PromoteSuperAdmin} --email <address> [--yes]");
            return 2;
        }

        using var scope = services.CreateScope();
        if (!await DemoSeedStartup.MigrationsAppliedAsync(scope, scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ServerCommands)), "Nothing was changed", cancellationToken))
        {
            await output.WriteLineAsync("The database is not up to date. Apply the migrations first (dotnet ef database update), then run the command again.");
            return 3;
        }

        if (!args.Contains("--yes", StringComparer.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync($"This makes {email.Trim()} a SuperAdmin: full control of accounts, roles and the admin area.");
            await output.WriteLineAsync("Type the address again to continue:");
            var typed = await input.ReadLineAsync(cancellationToken);
            if (!string.Equals(typed?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("The address did not match. Nothing was changed.");
                return 1;
            }
        }

        var result = await scope.ServiceProvider.GetRequiredService<ISuperAdminProvisioningService>().PromoteAsync(email, cancellationToken);
        await output.WriteLineAsync(Describe(result.Outcome));
        return result.Outcome == SuperAdminPromotionOutcome.Promoted ? 0 : 1;
    }

    private static string? ValueOf(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static string Describe(SuperAdminPromotionOutcome outcome) => outcome switch
    {
        SuperAdminPromotionOutcome.Promoted => "Done. The account is a SuperAdmin now; its open sessions have to sign in again.",
        SuperAdminPromotionOutcome.AlreadySuperAdmin => "That account is already a SuperAdmin. Nothing was changed.",
        SuperAdminPromotionOutcome.InvalidEmail => "That is not a valid e-mail address. Nothing was changed.",
        SuperAdminPromotionOutcome.NotFound => "No account uses that address. Nothing was changed.",
        SuperAdminPromotionOutcome.NotAnAdmin => "Only an Admin can be promoted. Make the account an Admin first. Nothing was changed.",
        SuperAdminPromotionOutcome.Inactive => "That account is deactivated. Activate it first. Nothing was changed.",
        SuperAdminPromotionOutcome.EmailNotVerified => "The e-mail address of that account was never confirmed. Nothing was changed.",
        _ => "Nothing was changed."
    };
}
