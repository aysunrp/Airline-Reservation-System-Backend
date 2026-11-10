using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

/// <summary>The state a token is checked against: read from the data layer through the Service, and remembered for a few seconds.</summary>
public class UserStateServiceTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

    private readonly FakeUserRepository _users = new(
        new User { Id = 1, Email = "a@example.com", Role = UserRole.Admin, IsActive = true, SessionsValidFrom = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc) });

    private UserStateService Create(int cacheSeconds = 30)
        => new(_users, new UserStateCache(Options.Create(new AuthSettings { UserStateCacheSeconds = cacheSeconds }), Clock));

    [Fact]
    public async Task TheStateOfAnAccount_IsItsRole_ItsActiveFlag_AndTheSignOutCutOff()
    {
        var state = await Create().GetAsync(1);

        Assert.NotNull(state);
        Assert.Equal(UserRole.Admin, state!.Role);
        Assert.True(state.IsActive);
        Assert.Equal(new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc), state.SessionsValidFrom);
    }

    [Fact]
    public async Task AnAccountThatDoesNotExist_HasNoState()
        => Assert.Null(await Create().GetAsync(99));

    [Fact]
    public async Task TheAnswer_IsRemembered_SoADeactivationShowsOnlyAfterTheCacheIsForgotten()
    {
        var service = Create();
        Assert.True((await service.GetAsync(1))!.IsActive);

        _users.Find(1)!.IsActive = false;

        // Remembered for a few seconds: the old answer. Without a cache the change shows at once.
        Assert.True((await service.GetAsync(1))!.IsActive);
        Assert.False((await Create(cacheSeconds: 0).GetAsync(1))!.IsActive);
    }
}
