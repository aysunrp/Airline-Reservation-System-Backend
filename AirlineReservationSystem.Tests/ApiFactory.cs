using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Repository.Data;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Starts the real API in memory against its own throwaway SQL Server database.
/// The database (AerovaDb_ApiTests_*) is created with the migrations before the tests of a class run and dropped afterwards,
/// so the development database (AerovaDb) is never touched. Set AEROVA_TEST_SQLSERVER to use another server than .\SQLEXPRESS.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "integration-tests-signing-key-that-is-long-enough";
    private const string DatabasePrefix = "AerovaDb_ApiTests_";

    private readonly string _databaseName = (DatabasePrefix + Guid.NewGuid().ToString("N"))[..40];

    public ApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("AEROVA_TEST_SQLSERVER") ?? @".\SQLEXPRESS";
        ConnectionString = $"Server={server};Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    }

    public string ConnectionString { get; }

    /// <summary>Signing key the API runs with; tests can set an invalid one to check the startup validation.</summary>
    public string JwtSecret { get; init; } = TestJwtSecret;

    /// <summary>Sign-in and register requests per minute and IP address; high so the many sign-ins of the tests never hit it.</summary>
    public int RateLimitPerMinute { get; init; } = 100_000;

    /// <summary>The e-mails the API would have sent.</summary>
    public TestEmailSender Emails => Services.GetRequiredService<TestEmailSender>();

    private Task<SeedData>? _seed;

    /// <summary>Writes the test flight network once per database and returns its ids (tests of a class run one after another).</summary>
    public Task<SeedData> GetSeedAsync() => _seed ??= TestSeed.CreateAsync(Services);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            // Tests never depend on the developer's user-secrets.
            services.PostConfigure<JwtSettings>(settings => settings.Secret = JwtSecret);

            // Cheap password hashes and a generous request limit keep the test run fast; the tests of those two features set their own.
            // Booking does not wait for a confirmed e-mail here, so the many tests that register and book need no mailbox.
            services.PostConfigure<AuthSettings>(settings =>
            {
                settings.PasswordHashIterations = 1_000;
                settings.RateLimitPerMinute = RateLimitPerMinute;
                settings.RequireVerifiedEmailForBooking = false;
                settings.FrontendBaseUrl = "https://aerova.test";
            });

            // No real SMTP in tests: the messages end up in a list the tests read.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<TestEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<TestEmailSender>());

            // Tests trigger generation themselves; a background run would change their flight counts.
            services.PostConfigure<FlightGenerationSettings>(settings => settings.WorkerEnabled = false);
            services.PostConfigure<BookingLifecycleSettings>(settings => settings.WorkerEnabled = false);

            // A developer's local Seed:* user-secrets must not seed test databases; tests switch these on themselves.
            services.PostConfigure<SeedSettings>(settings =>
            {
                settings.Enabled = false;
                settings.CreateAdmin = false;
                settings.AdminEmail = null;
                settings.AdminPassword = null;
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Guard: only ever drop the database this factory created.
        if (_databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }
}
