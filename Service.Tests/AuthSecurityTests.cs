using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

/// <summary>Sign-in protection: paused sign-in after repeated failures, password hashes that keep up with the iteration count, and a log without secrets.</summary>
public class AuthSecurityTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly JwtSettings Jwt = new()
    {
        Issuer = "AEROVA",
        Audience = "AEROVA.Client",
        Secret = "test-secret-that-is-long-enough-for-hmac-sha256!!",
        ExpiryMinutes = 60
    };

    private readonly AdjustableTimeProvider _clock = new(Start);
    private readonly ListLogger<AuthService> _log = new();

    private AuthService CreateService(FakeUserRepository users, AuthSettings? settings = null, PasswordHasher? hasher = null)
    {
        var auth = Options.Create(settings ?? new AuthSettings { PasswordHashIterations = 1_000 });

        return new AuthService(
            users,
            hasher ?? new PasswordHasher(auth),
            new TokenService(Options.Create(Jwt), _clock),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new LoginAttemptTracker(auth, _clock),
            auth,
            _log);
    }

    private static User Aysun(string password, PasswordHasher? hasher = null) => new()
    {
        Id = 5,
        Email = "aysun@example.com",
        FirstName = "Aysun",
        LastName = "Panahli",
        PasswordHash = (hasher ?? new PasswordHasher(Options.Create(new AuthSettings { PasswordHashIterations = 1_000 }))).Hash(password)
    };

    private static LoginRequestDto Login(string email, string password) => new() { Email = email, Password = password };

    private static async Task Fail(AuthService service, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(Login(email, "wrong-password")));
        }
    }

    [Fact]
    public async Task FiveFailures_PauseSignIn_EvenForTheRightPassword_UntilThePauseEnds()
    {
        var service = CreateService(new FakeUserRepository(Aysun("secret1")));

        await Fail(service, "aysun@example.com", 5);

        var locked = await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("AYSUN@example.com", "secret1")));
        Assert.Equal(TimeSpan.FromMinutes(15), locked.RetryAfter);
        Assert.Contains("15 minutes", locked.Message);

        _clock.Advance(TimeSpan.FromMinutes(14));
        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("aysun@example.com", "secret1")));

        _clock.Advance(TimeSpan.FromMinutes(1.1));
        Assert.Equal(5, (await service.LoginAsync(Login("aysun@example.com", "secret1"))).User.Id);
    }

    [Fact]
    public async Task AnAddressWithoutAnAccount_IsPausedTheSameWay_SoNobodyCanTellWhichAddressesExist()
    {
        var service = CreateService(new FakeUserRepository(Aysun("secret1")));

        await Fail(service, "nobody@example.com", 5);

        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("nobody@example.com", "secret1")));
        // Another address is not affected.
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(Login("other@example.com", "wrong-password")));
    }

    [Fact]
    public async Task ASuccessfulSignIn_ForgetsEarlierFailures()
    {
        var service = CreateService(new FakeUserRepository(Aysun("secret1")));

        await Fail(service, "aysun@example.com", 4);
        await service.LoginAsync(Login("aysun@example.com", "secret1"));
        await Fail(service, "aysun@example.com", 4);

        // Still not paused: only 4 failures since the last success.
        Assert.NotNull(await service.LoginAsync(Login("aysun@example.com", "secret1")));
    }

    [Fact]
    public async Task OldFailures_StopCounting_AfterAFullWindowWithoutNewOnes()
    {
        var service = CreateService(new FakeUserRepository(Aysun("secret1")));

        await Fail(service, "aysun@example.com", 4);
        _clock.Advance(TimeSpan.FromMinutes(16));
        await Fail(service, "aysun@example.com", 1);

        Assert.NotNull(await service.LoginAsync(Login("aysun@example.com", "secret1")));
    }

    [Fact]
    public async Task AWeakerHash_IsUpgraded_WhenItsOwnerSignsIn()
    {
        var oldHasher = new PasswordHasher(Options.Create(new AuthSettings { PasswordHashIterations = 1_000 }));
        var user = Aysun("secret1", oldHasher);
        var users = new FakeUserRepository(user);
        var service = CreateService(users, new AuthSettings { PasswordHashIterations = 3_000 });

        Assert.StartsWith("v1.1000.", user.PasswordHash);

        await service.LoginAsync(Login("aysun@example.com", "secret1"));

        Assert.StartsWith("v1.3000.", user.PasswordHash);
        Assert.True(new PasswordHasher(Options.Create(new AuthSettings { PasswordHashIterations = 3_000 })).Verify("secret1", user.PasswordHash));
        // And the upgraded hash keeps working.
        await service.LoginAsync(Login("aysun@example.com", "secret1"));
        Assert.StartsWith("v1.3000.", user.PasswordHash);
    }

    [Fact]
    public async Task AWrongPassword_DoesNotUpgradeTheHash()
    {
        var user = Aysun("secret1");
        var service = CreateService(new FakeUserRepository(user), new AuthSettings { PasswordHashIterations = 3_000 });
        var before = user.PasswordHash;

        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(Login("aysun@example.com", "wrong-password")));

        Assert.Equal(before, user.PasswordHash);
    }

    [Fact]
    public void NewHashes_UseTheConfiguredIterations_WithASafetyFloor()
    {
        Assert.StartsWith("v1.7000.", new PasswordHasher(Options.Create(new AuthSettings { PasswordHashIterations = 7_000 })).Hash("secret1"));
        Assert.StartsWith("v1.1000.", new PasswordHasher(Options.Create(new AuthSettings { PasswordHashIterations = 1 })).Hash("secret1"));
        Assert.StartsWith("v1.600000.", new PasswordHasher().Hash("secret1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("v2.1000.AA==.AA==")]
    public void NeedsRehash_IsFalse_ForHashesThatAreNotValidVersionOneHashes(string hash)
    {
        Assert.False(new PasswordHasher().NeedsRehash(hash));
    }

    [Fact]
    public async Task TheLog_NeverHoldsTheAddressOrThePassword()
    {
        var service = CreateService(new FakeUserRepository(Aysun("secret1")));

        await Fail(service, "aysun@example.com", 5);
        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("aysun@example.com", "my-typed-password")));

        Assert.Contains(_log.Messages, message => message.Contains("paused", StringComparison.OrdinalIgnoreCase));
        Assert.All(_log.Messages, message =>
        {
            Assert.DoesNotContain("aysun", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret1", message);
            Assert.DoesNotContain("wrong-password", message);
            Assert.DoesNotContain("my-typed-password", message);
        });
    }

    [Fact]
    public void ARegistrationHasNoRoleToChoose()
    {
        Assert.Null(typeof(RegisterRequestDto).GetProperty("Role"));
        Assert.Null(typeof(RegisterRequestDto).GetProperty("IsActive"));
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public AdjustableTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
