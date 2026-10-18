using System.Net;
using System.Net.Http.Json;
using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Data;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Settings;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The development admin step at startup: off by default, Development only, idempotent, and silent about credentials.</summary>
public class DevelopmentAdminStartupTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    // Generated per run; test values only, never written to configuration files.
    private readonly string _email = $"dev.admin.{Guid.NewGuid():N}@example.com";
    private readonly string _password = $"Adm{Guid.NewGuid():N}"[..20];

    public DevelopmentAdminStartupTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class Environment : IHostEnvironment
    {
        public Environment(string name) => EnvironmentName = name;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "AirlineReservationSystem";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private static readonly IHostEnvironment Development = new Environment(Environments.Development);

    private async Task<DevelopmentAdminResultDto?> RunAsync(ListLogger logger, IHostEnvironment environment, bool enabled, string? email, string? password)
    {
        var settings = _factory.Services.GetRequiredService<IOptions<SeedSettings>>().Value;
        settings.CreateAdmin = enabled;
        settings.AdminEmail = email;
        settings.AdminPassword = password;
        try
        {
            return await DemoSeedStartup.CreateAdminAsync(_factory.Services, environment, logger);
        }
        finally
        {
            settings.CreateAdmin = false;
            settings.AdminEmail = null;
            settings.AdminPassword = null;
        }
    }

    private async Task<int> AccountsWithAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(u => u.Email == email);
    }

    [Fact]
    public async Task IsOffByDefault()
    {
        var settings = _factory.Services.GetRequiredService<IOptions<SeedSettings>>().Value;
        Assert.False(settings.CreateAdmin);
        Assert.Null(settings.AdminEmail);
        Assert.Null(settings.AdminPassword);

        Assert.Null(await DemoSeedStartup.CreateAdminAsync(_factory.Services, Development, new ListLogger()));
    }

    [Fact]
    public async Task OutsideDevelopment_NoAccountIsCreated_EvenWhenEnabled()
    {
        var logger = new ListLogger();

        Assert.Null(await RunAsync(logger, new Environment(Environments.Production), true, _email, _password));

        Assert.Equal(0, await AccountsWithAsync(_email));
        Assert.Contains(logger.Messages, m => m.Contains("ignored outside the Development environment"));
    }

    [Fact]
    public async Task InDevelopment_TheAdminIsCreatedOnce_AndCanSignIn()
    {
        var logger = new ListLogger();

        var first = await RunAsync(logger, Development, true, _email, _password);
        var second = await RunAsync(logger, Development, true, _email, _password);

        Assert.Equal(DevelopmentAdminOutcome.Created, first!.Outcome);
        Assert.Equal(DevelopmentAdminOutcome.AlreadyAdmin, second!.Outcome);
        Assert.Equal(1, await AccountsWithAsync(_email));

        using (var scope = _factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Email == _email);
            Assert.Equal(UserRole.Admin, user.Role);
            Assert.NotEqual(_password, user.PasswordHash);
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = _email, Password = _password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.ReadAsync<AuthResponseDto>();
        Assert.Equal("Admin", auth.User.Role);
        Assert.Equal(HttpStatusCode.OK, (await client.WithToken(auth.AccessToken).GetAsync("/api/admin/dashboard")).StatusCode);

        // Neither the email nor the password is ever logged.
        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, m =>
        {
            Assert.DoesNotContain(_email, m, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_password, m);
        });
    }

    [Fact]
    public async Task ExistingClient_IsNotPromoted()
    {
        await _factory.CreateClient().RegisterAsync(_email, "client-pass1");

        var result = await RunAsync(new ListLogger(), Development, true, _email, _password);

        Assert.Equal(DevelopmentAdminOutcome.ExistingNonAdmin, result!.Outcome);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(UserRole.Client, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Email == _email)).Role);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = _email, Password = _password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task InvalidConfiguration_IsReportedWithoutTheValues()
    {
        var logger = new ListLogger();

        var result = await RunAsync(logger, Development, true, _email, "short1");

        Assert.Equal(DevelopmentAdminOutcome.InvalidConfiguration, result!.Outcome);
        Assert.Equal(0, await AccountsWithAsync(_email));
        var warning = Assert.Single(logger.Messages, m => m.Contains("Seed:AdminPassword"));
        Assert.DoesNotContain("short1", warning);
        Assert.DoesNotContain(_email, warning, StringComparison.OrdinalIgnoreCase);
    }
}
