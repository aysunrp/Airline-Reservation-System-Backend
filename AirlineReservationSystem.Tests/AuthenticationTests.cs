using System.Security.Claims;
using System.Text.Json;
using AirlineReservationSystem.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

public class AuthenticationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthenticationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static User Admin => new() { Id = 42, Email = "admin@aerova.az", FirstName = "Aerova", LastName = "Admin", Role = UserRole.Admin };

    /// <summary>Validates a token exactly the way the API's JwtBearer handler is configured to.</summary>
    private async Task<(bool IsValid, ClaimsPrincipal? Principal)> ValidateAsync(string token)
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var result = await options.TokenHandlers.First().ValidateTokenAsync(token, options.TokenValidationParameters);
        return (result.IsValid, result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null);
    }

    [Fact]
    public async Task IssuedToken_IsAcceptedWithUserIdAndRole()
    {
        using var scope = _factory.Services.CreateScope();
        var (token, _) = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateToken(Admin);

        var (isValid, principal) = await ValidateAsync(token);

        Assert.True(isValid);
        Assert.NotNull(principal);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole(nameof(UserRole.Admin)));
    }

    [Fact]
    public async Task AdminPolicy_RequiresTheAdminRole()
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var client = Admin;
        client.Role = UserRole.Client;

        var (_, adminPrincipal) = await ValidateAsync(tokens.CreateToken(Admin).AccessToken);
        var (_, clientPrincipal) = await ValidateAsync(tokens.CreateToken(client).AccessToken);

        Assert.True((await authorization.AuthorizeAsync(adminPrincipal!, AuthorizationPolicies.Admin)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(clientPrincipal!, AuthorizationPolicies.Admin)).Succeeded);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var settings = _factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var forger = new TokenService(Options.Create(new JwtSettings
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Secret = "a-completely-different-signing-key-of-enough-length",
            ExpiryMinutes = 60
        }), TimeProvider.System);

        Assert.False((await ValidateAsync(forger.CreateToken(Admin).AccessToken)).IsValid);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var settings = _factory.Services.GetRequiredService<IOptions<JwtSettings>>();
        var lastWeek = new FixedTimeProvider(DateTime.UtcNow.AddDays(-7));
        var expired = new TokenService(settings, lastWeek).CreateToken(Admin).AccessToken;

        Assert.False((await ValidateAsync(expired)).IsValid);
    }

    [Fact]
    public async Task Swagger_DescribesTheBearerScheme()
    {
        var json = await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("AEROVA API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
    }

    [Fact]
    public async Task ApiDoesNotStart_WithoutAUsableSigningKey()
    {
        await using var factory = new ApiFactory { JwtSecret = "too-short" };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:Secret", error.ToString());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
