using Domain.Entities;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

/// <summary>Sign-in protection: paused sign-in after repeated failures, password hashes that keep up with the iteration count, and a log without secrets.</summary>
public class AuthSecurityTests
{
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
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAuth();

        await Fail(service, "aysun@example.com", 5);

        var locked = await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("AYSUN@example.com", "secret1")));
        Assert.Equal(TimeSpan.FromMinutes(15), locked.RetryAfter);
        Assert.Contains("15 minutes", locked.Message);

        context.Clock.Advance(TimeSpan.FromMinutes(14));
        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("aysun@example.com", "secret1")));

        context.Clock.Advance(TimeSpan.FromMinutes(1.1));
        Assert.Equal(5, (await service.LoginAsync(Login("aysun@example.com", "secret1"))).User.Id);
    }

    [Fact]
    public async Task AnAddressWithoutAnAccount_IsPausedTheSameWay_SoNobodyCanTellWhichAddressesExist()
    {
        var service = new AccountTestContext(AccountTestContext.Customer("secret1")).CreateAuth();

        await Fail(service, "nobody@example.com", 5);

        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("nobody@example.com", "secret1")));
        // Another address is not affected.
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(Login("other@example.com", "wrong-password")));
    }

    [Fact]
    public async Task ASuccessfulSignIn_ForgetsEarlierFailures()
    {
        var service = new AccountTestContext(AccountTestContext.Customer("secret1")).CreateAuth();

        await Fail(service, "aysun@example.com", 4);
        await service.LoginAsync(Login("aysun@example.com", "secret1"));
        await Fail(service, "aysun@example.com", 4);

        // Still not paused: only 4 failures since the last success.
        Assert.NotNull(await service.LoginAsync(Login("aysun@example.com", "secret1")));
    }

    [Fact]
    public async Task OldFailures_StopCounting_AfterAFullWindowWithoutNewOnes()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAuth();

        await Fail(service, "aysun@example.com", 4);
        context.Clock.Advance(TimeSpan.FromMinutes(16));
        await Fail(service, "aysun@example.com", 1);

        Assert.NotNull(await service.LoginAsync(Login("aysun@example.com", "secret1")));
    }

    [Fact]
    public async Task AWeakerHash_IsUpgraded_WhenItsOwnerSignsIn()
    {
        var user = AccountTestContext.Customer("secret1");
        var context = new AccountTestContext(user);
        context.Settings.PasswordHashIterations = 3_000;
        var service = context.CreateAuth();

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
        var user = AccountTestContext.Customer("secret1");
        var context = new AccountTestContext(user);
        context.Settings.PasswordHashIterations = 3_000;
        var before = user.PasswordHash;

        await Assert.ThrowsAsync<UnauthorizedException>(() => context.CreateAuth().LoginAsync(Login("aysun@example.com", "wrong-password")));

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
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAuth();

        await Fail(service, "aysun@example.com", 5);
        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.LoginAsync(Login("aysun@example.com", "my-typed-password")));

        Assert.Contains(context.AuthLog.Messages, message => message.Contains("paused", StringComparison.OrdinalIgnoreCase));
        Assert.All(context.AuthLog.Messages, message =>
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
        Assert.Null(typeof(RegisterRequestDto).GetProperty("EmailVerifiedAt"));
    }
}
