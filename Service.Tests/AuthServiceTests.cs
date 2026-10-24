using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class AuthServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly JwtSettings Jwt = new()
    {
        Issuer = "AEROVA",
        Audience = "AEROVA.Client",
        Secret = "test-secret-that-is-long-enough-for-hmac-sha256!!",
        ExpiryMinutes = 60
    };

    private readonly PasswordHasher _hasher = new();

    private AuthService CreateService(FakeUserRepository users, JwtSettings? jwt = null)
        => new(users, _hasher, new TokenService(Options.Create(jwt ?? Jwt), new FixedTimeProvider(Now)),
            new RegisterRequestValidator(), new LoginRequestValidator(),
            new LoginAttemptTracker(Options.Create(new AuthSettings()), new FixedTimeProvider(Now)),
            Options.Create(new AuthSettings()), NullLogger<AuthService>.Instance);

    private static RegisterRequestDto ValidRegistration() => new()
    {
        FirstName = " Aysun ",
        LastName = "Panahli",
        Email = " Aysun@Example.com ",
        Password = "secret1",
        ConfirmPassword = "secret1"
    };

    [Fact]
    public async Task Register_CreatesClientWithHashedPasswordAndReturnsToken()
    {
        var users = new FakeUserRepository();
        var result = await CreateService(users).RegisterAsync(ValidRegistration());

        var user = Assert.Single(await users.GetAllAsync());
        Assert.Equal("aysun@example.com", user.Email);
        Assert.Equal("Aysun", user.FirstName);
        Assert.Equal(UserRole.Client, user.Role);
        Assert.NotEqual("secret1", user.PasswordHash);
        Assert.True(_hasher.Verify("secret1", user.PasswordHash));

        Assert.Equal(Now.AddMinutes(60), result.ExpiresAt);
        Assert.Equal("aysun@example.com", result.User.Email);
        Assert.Equal("Client", result.User.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal("AEROVA", token.Issuer);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Client");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        var users = new FakeUserRepository(new User { Email = "aysun@example.com", PasswordHash = "x" });

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(users).RegisterAsync(ValidRegistration()));
    }

    [Theory]
    [InlineData("12345", "12345")]      // shorter than 6
    [InlineData("secret1", "secret2")]  // confirmation mismatch
    public async Task Register_InvalidPassword_ThrowsValidation(string password, string confirm)
    {
        var request = ValidRegistration();
        request.Password = password;
        request.ConfirmPassword = confirm;

        await Assert.ThrowsAsync<ValidationException>(() => CreateService(new FakeUserRepository()).RegisterAsync(request));
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var users = new FakeUserRepository(new User { Id = 5, Email = "aysun@example.com", PasswordHash = _hasher.Hash("secret1"), FirstName = "Aysun", LastName = "Panahli" });

        var result = await CreateService(users).LoginAsync(new LoginRequestDto { Email = "AYSUN@example.com", Password = "secret1" });

        Assert.Equal(5, result.User.Id);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
    }

    [Theory]
    [InlineData("aysun@example.com", "wrong-password", true)]
    [InlineData("nobody@example.com", "secret1", true)]
    [InlineData("aysun@example.com", "secret1", false)] // account disabled
    public async Task Login_Failures_ThrowUnauthorized(string email, string password, bool isActive)
    {
        var users = new FakeUserRepository(new User { Email = "aysun@example.com", PasswordHash = _hasher.Hash("secret1"), IsActive = isActive });

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService(users).LoginAsync(new LoginRequestDto { Email = email, Password = password }));
    }

    [Fact]
    public async Task MissingJwtSecret_FailsClearly()
    {
        var service = CreateService(new FakeUserRepository(), new JwtSettings { Secret = "short" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(ValidRegistration()));
    }
}

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_IsSaltedAndVerifiable()
    {
        var first = _hasher.Hash("secret1");
        var second = _hasher.Hash("secret1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("secret1", first));
        Assert.True(_hasher.Verify("secret1", second));
        Assert.False(_hasher.Verify("Secret1", first));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v1.100000.not-base64!.not-base64!")]
    public void Verify_MalformedHash_ReturnsFalse(string storedHash)
    {
        Assert.False(_hasher.Verify("secret1", storedHash));
    }
}
