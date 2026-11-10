using System.Net;
using System.Net.Http.Json;
using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Data;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Settings;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// The first SuperAdmin: made only from server configuration, once, never by promoting an account by itself, and silent about
/// credentials; and the operator command that promotes one existing Admin after a typed confirmation.
/// </summary>
public class SuperAdminProvisioningStartupTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;

    // Generated per run; test values only, never written to configuration files.
    private readonly string _email = $"first.super.{Guid.NewGuid():N}@example.com";
    private readonly string _password = $"Sup{Guid.NewGuid():N}"[..24];

    public SuperAdminProvisioningStartupTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    /// <summary>The tests of this class share one database, and each of them needs to start with no SuperAdmin in it.</summary>
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.RemoveRange(await db.Users.Where(u => u.Role == UserRole.SuperAdmin).ToListAsync());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<SuperAdminBootstrapResultDto?> RunAsync(ListLogger logger, bool enabled, string? email, string? password)
    {
        var settings = _factory.Services.GetRequiredService<IOptions<BootstrapSettings>>().Value;
        settings.Enabled = enabled;
        settings.SuperAdminEmail = email;
        settings.SuperAdminPassword = password;
        try
        {
            return await SuperAdminStartup.RunAsync(_factory.Services, logger);
        }
        finally
        {
            settings.Enabled = false;
            settings.SuperAdminEmail = null;
            settings.SuperAdminPassword = null;
        }
    }

    private async Task<(UserRole Role, bool IsActive)?> AccountAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);
        return user is null ? null : (user.Role, user.IsActive);
    }

    [Fact]
    public async Task IsOffByDefault()
    {
        var settings = _factory.Services.GetRequiredService<IOptions<BootstrapSettings>>().Value;
        Assert.False(settings.Enabled);
        Assert.Null(settings.SuperAdminEmail);
        Assert.Null(settings.SuperAdminPassword);

        Assert.Null(await SuperAdminStartup.RunAsync(_factory.Services, new ListLogger()));
    }

    [Fact]
    public async Task WhenEnabled_TheSuperAdminIsCreatedOnce_AndCanSignInAndManageAccounts()
    {
        var logger = new ListLogger();

        var first = await RunAsync(logger, true, _email, _password);
        var second = await RunAsync(logger, true, _email, _password);

        Assert.Equal(SuperAdminBootstrapOutcome.Created, first!.Outcome);
        Assert.Equal(SuperAdminBootstrapOutcome.AlreadyExists, second!.Outcome);
        Assert.Equal(UserRole.SuperAdmin, (await AccountAsync(_email))!.Value.Role);

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = _email, Password = _password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.ReadAsync<AuthResponseDto>();
        Assert.Equal(("SuperAdmin", true), (auth.User.Role, auth.User.EmailVerified));
        Assert.Equal(HttpStatusCode.OK, (await client.WithToken(auth.AccessToken).GetAsync("/api/admin/users")).StatusCode);

        // The trail names the server, not a person.
        using (var scope = _factory.Services.CreateScope())
        {
            var entry = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
                .SingleAsync(a => a.Action == AuditActions.SuperAdminBootstrapped && a.TargetId == auth.User.Id.ToString());
            Assert.Null(entry.ActorUserId);
            Assert.Equal("Server", entry.ActorRole);
        }

        // Neither the email nor the password is ever logged.
        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, m =>
        {
            Assert.DoesNotContain(_email, m, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_password, m);
        });
    }

    [Fact]
    public async Task ASettingLeftBehind_CannotAddAnotherSuperAdmin()
    {
        await RunAsync(new ListLogger(), true, _email, _password);
        var another = $"another.{Guid.NewGuid():N}@example.com";

        var result = await RunAsync(new ListLogger(), true, another, _password);

        Assert.Equal(SuperAdminBootstrapOutcome.AlreadyExists, result!.Outcome);
        Assert.Null(await AccountAsync(another));
    }

    [Fact]
    public async Task AnExistingAccount_IsNeverPromoted()
    {
        await _factory.CreateClient().RegisterAsync(_email, "client-pass1");

        var result = await RunAsync(new ListLogger(), true, _email, _password);

        Assert.Equal(SuperAdminBootstrapOutcome.EmailTaken, result!.Outcome);
        Assert.Equal(UserRole.User, (await AccountAsync(_email))!.Value.Role);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = _email, Password = _password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task InvalidConfiguration_IsReportedWithoutTheValues_AndStoresNothing()
    {
        var logger = new ListLogger();

        var result = await RunAsync(logger, true, _email, "short1");

        Assert.Equal(SuperAdminBootstrapOutcome.InvalidConfiguration, result!.Outcome);
        Assert.Null(await AccountAsync(_email));
        Assert.Contains(logger.Messages, m => m.Contains("Bootstrap:SuperAdminPassword"));
        Assert.All(logger.Messages, m =>
        {
            Assert.DoesNotContain(_email, m, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("short1", m);
        });
    }

    private async Task<(int Id, string Email)> AdminAccountAsync(bool confirmed = true)
    {
        var account = await _factory.CreateAccountAsync(UserRole.Admin);
        if (confirmed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(u => u.Id == account.Id)).EmailVerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return (account.Id, account.Email);
    }

    private async Task<int> RunCommandAsync(string input, params string[] args)
        => (await RunCommandWithOutputAsync(input, args)).Code;

    private async Task<(int Code, string Output)> RunCommandWithOutputAsync(string input, params string[] args)
    {
        var output = new StringWriter();
        var code = await ServerCommands.RunAsync(args, _factory.Services, new StringReader(input), output);
        return (code, output.ToString());
    }

    [Fact]
    public async Task PromoteCommand_AfterTypingTheAddressAgain_MakesTheAdminASuperAdmin()
    {
        var (id, email) = await AdminAccountAsync();

        var code = await RunCommandAsync(email + Environment.NewLine, ServerCommands.PromoteSuperAdmin, "--email", email);

        Assert.Equal(0, code);
        Assert.Equal(UserRole.SuperAdmin, (await AccountAsync(email))!.Value.Role);

        // A new sign-in carries the new role.
        _factory.Services.GetRequiredService<IUserStateCache>().Invalidate(id);
        var token = await _factory.CreateClient().SignInAsync(email, "admin-pass");
        Assert.Equal(HttpStatusCode.OK, (await _factory.ClientWith(token).GetAsync("/api/admin/users")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var entry = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .SingleAsync(a => a.Action == AuditActions.UserRoleChanged && a.TargetId == id.ToString());
        Assert.Equal("Server", entry.ActorRole);
    }

    [Fact]
    public async Task PromoteCommand_WithAWrongOrMissingConfirmation_ChangesNothing()
    {
        var (_, email) = await AdminAccountAsync();

        Assert.Equal(1, await RunCommandAsync("someone.else@example.com" + Environment.NewLine, ServerCommands.PromoteSuperAdmin, "--email", email));
        Assert.Equal(1, await RunCommandAsync(string.Empty, ServerCommands.PromoteSuperAdmin, "--email", email));

        Assert.Equal(UserRole.Admin, (await AccountAsync(email))!.Value.Role);
    }

    [Fact]
    public async Task PromoteCommand_YesSkipsTheQuestion_ButNotTheChecks()
    {
        var (_, confirmed) = await AdminAccountAsync();
        var (_, unconfirmed) = await AdminAccountAsync(confirmed: false);
        var user = await _factory.CreateClient().RegisterAsync();

        Assert.Equal(0, await RunCommandAsync(string.Empty, ServerCommands.PromoteSuperAdmin, "--email", confirmed, "--yes"));
        Assert.Equal(1, await RunCommandAsync(string.Empty, ServerCommands.PromoteSuperAdmin, "--email", unconfirmed, "--yes"));
        Assert.Equal(1, await RunCommandAsync(string.Empty, ServerCommands.PromoteSuperAdmin, "--email", user.User.Email, "--yes"));
        Assert.Equal(1, await RunCommandAsync(string.Empty, ServerCommands.PromoteSuperAdmin, "--email", "nobody@example.com", "--yes"));

        Assert.Equal(UserRole.SuperAdmin, (await AccountAsync(confirmed))!.Value.Role);
        Assert.Equal(UserRole.Admin, (await AccountAsync(unconfirmed))!.Value.Role);
        Assert.Equal(UserRole.User, (await AccountAsync(user.User.Email))!.Value.Role);
    }

    [Fact]
    public async Task PromoteCommand_WithoutAnAddress_ShowsTheUsage()
    {
        var (code, output) = await RunCommandWithOutputAsync(string.Empty, ServerCommands.PromoteSuperAdmin);

        Assert.Equal(2, code);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void OnlyTheCommandNameStartsTheCommand()
    {
        Assert.True(ServerCommands.IsCommand(new[] { "promote-superadmin", "--email", "a@b.c" }));
        Assert.False(ServerCommands.IsCommand(Array.Empty<string>()));
        Assert.False(ServerCommands.IsCommand(new[] { "--urls", "http://localhost:5080" }));
    }
}
