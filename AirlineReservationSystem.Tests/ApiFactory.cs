using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Repository.Data;
using Service.Helpers.Settings;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            // Tests never depend on the developer's user-secrets.
            services.PostConfigure<JwtSettings>(settings => settings.Secret = TestJwtSecret);
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
