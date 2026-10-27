using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

public class AdminUserServiceTests
{
    private static readonly AuditActor SuperAdminActor = new(1, RoleNames.SuperAdmin);

    private readonly AdjustableTimeProvider _clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeAuditLogRepository _logs = new();
    private readonly UserStateCache _cache;
    private readonly PasswordHasher _hasher = new();
    private readonly FakeUserRepository _users;

    public AdminUserServiceTests()
    {
        _cache = new UserStateCache(Options.Create(new AuthSettings { UserStateCacheSeconds = 30 }), _clock);
        _users = new FakeUserRepository(
            new User { Id = 1, Email = "super@example.com", FirstName = "Super", LastName = "Admin", Role = UserRole.SuperAdmin },
            new User { Id = 2, Email = "admin@example.com", FirstName = "Ada", LastName = "Admin", Role = UserRole.Admin },
            new User { Id = 3, Email = "user@example.com", FirstName = "Ulvi", LastName = "User", Role = UserRole.User });
    }

    private AdminUserService CreateService()
        => new(_users, _hasher, new AuditLogService(_logs, new ListLogger<AuditLogService>()), _cache, new CreateAdminRequestValidator(), _clock);

    private static CreateAdminRequestDto NewAdmin(string email = "new.admin@example.com", string password = "Passw0rd99") => new()
    {
        Email = email,
        FirstName = "New",
        LastName = "Admin",
        Password = password
    };

    [Fact]
    public async Task CreateAdmin_MakesAConfirmedAdminWithAHashedPassword_AndLogsIt()
    {
        var created = await CreateService().CreateAdminAsync(SuperAdminActor, NewAdmin(" New.Admin@Example.com "));

        Assert.Equal(("new.admin@example.com", "Admin", true, true), (created.Email, created.Role, created.IsActive, created.EmailVerified));
        var stored = _users.Find(created.Id)!;
        Assert.DoesNotContain("Passw0rd99", stored.PasswordHash);
        Assert.True(_hasher.Verify("Passw0rd99", stored.PasswordHash));

        var entry = Assert.Single(_logs.Entries);
        Assert.Equal((AuditActions.UserCreated, "User", created.Id.ToString(), 1, RoleNames.SuperAdmin), (entry.Action, entry.TargetType, entry.TargetId, entry.ActorUserId, entry.ActorRole));
    }

    [Fact]
    public async Task CreateAdmin_ExistingEmail_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => CreateService().CreateAdminAsync(SuperAdminActor, NewAdmin("USER@example.com")));
        Assert.Empty(_logs.Entries);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task CreateAdmin_WeakPassword_IsRejected(string password)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAdminAsync(SuperAdminActor, NewAdmin(password: password)));
    }

    [Fact]
    public async Task ChangeRole_GrantsAndTakesBackAdmin_AndLogsBoth()
    {
        var service = CreateService();

        var granted = await service.ChangeRoleAsync(SuperAdminActor, 3, UserRole.Admin);
        var revoked = await service.ChangeRoleAsync(SuperAdminActor, 3, UserRole.User);

        Assert.Equal(("Admin", "User"), (granted.Role, revoked.Role));
        Assert.Equal(new[] { "User -> Admin", "Admin -> User" }, _logs.Entries.Select(e => e.Details));
        Assert.All(_logs.Entries, e => Assert.Equal(AuditActions.UserRoleChanged, e.Action));
    }

    [Fact]
    public async Task ChangeRole_ToTheSameRole_ChangesAndLogsNothing()
    {
        await CreateService().ChangeRoleAsync(SuperAdminActor, 2, UserRole.Admin);

        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task ChangeRole_CannotGrantSuperAdmin()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ChangeRoleAsync(SuperAdminActor, 3, UserRole.SuperAdmin));
        Assert.Equal(UserRole.User, _users.Find(3)!.Role);
    }

    [Fact]
    public async Task ChangeRole_CannotChangeASuperAdmin()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ChangeRoleAsync(new AuditActor(2, RoleNames.Admin), 1, UserRole.User));
        Assert.Equal(UserRole.SuperAdmin, _users.Find(1)!.Role);
    }

    [Fact]
    public async Task ChangeRole_UnknownAccount_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().ChangeRoleAsync(SuperAdminActor, 99, UserRole.Admin));
    }

    [Fact]
    public async Task ChangeRole_ForgetsTheRememberedStateOfTheAccount()
    {
        _cache.Set(3, new UserState(UserRole.User, true));

        await CreateService().ChangeRoleAsync(SuperAdminActor, 3, UserRole.Admin);

        Assert.False(_cache.TryGet(3, out _));
    }

    [Fact]
    public async Task Deactivate_BlocksTheAccount_LogsIt_AndForgetsItsState()
    {
        _cache.Set(3, new UserState(UserRole.User, true));

        var result = await CreateService().SetActiveAsync(SuperAdminActor, 3, false);

        Assert.False(result.IsActive);
        Assert.False(_users.Find(3)!.IsActive);
        Assert.Equal(AuditActions.UserDeactivated, Assert.Single(_logs.Entries).Action);
        Assert.False(_cache.TryGet(3, out _));
    }

    [Fact]
    public async Task Activate_AgainRestoresTheAccount()
    {
        var service = CreateService();
        await service.SetActiveAsync(SuperAdminActor, 3, false);

        await service.SetActiveAsync(SuperAdminActor, 3, true);

        Assert.True(_users.Find(3)!.IsActive);
        Assert.Equal(new[] { AuditActions.UserDeactivated, AuditActions.UserActivated }, _logs.Entries.Select(e => e.Action));
    }

    [Fact]
    public async Task Deactivate_YourOwnAccount_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().SetActiveAsync(SuperAdminActor, 1, false));
        Assert.True(_users.Find(1)!.IsActive);
    }

    [Fact]
    public async Task Deactivate_TheLastActiveSuperAdmin_IsRefused()
    {
        // Another account (an Admin id here) asks; the only SuperAdmin must stay.
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().SetActiveAsync(new AuditActor(2, RoleNames.Admin), 1, false));
        Assert.True(_users.Find(1)!.IsActive);
    }

    [Fact]
    public async Task Deactivate_ASuperAdmin_IsAllowedWhileAnotherActiveOneRemains()
    {
        await _users.AddAsync(new User { Id = 4, Email = "second@example.com", FirstName = "Second", LastName = "Super", Role = UserRole.SuperAdmin });

        var result = await CreateService().SetActiveAsync(SuperAdminActor, 4, false);

        Assert.False(result.IsActive);
        // ... and now the other one is the last.
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().SetActiveAsync(new AuditActor(4, RoleNames.SuperAdmin), 1, false));
    }

    [Fact]
    public async Task Delete_AccountWithoutBookings_RemovesItAndLogsIt()
    {
        await CreateService().DeleteAsync(SuperAdminActor, 3);

        Assert.Null(_users.Find(3));
        var entry = Assert.Single(_logs.Entries);
        Assert.Equal((AuditActions.UserDeleted, "3"), (entry.Action, entry.TargetId));
    }

    [Fact]
    public async Task Delete_AccountWithBookings_IsRefused()
    {
        _users.WithBookings.Add(3);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().DeleteAsync(SuperAdminActor, 3));
        Assert.NotNull(_users.Find(3));
    }

    [Fact]
    public async Task Delete_YourselfOrTheLastSuperAdmin_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().DeleteAsync(SuperAdminActor, 1));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().DeleteAsync(new AuditActor(2, RoleNames.Admin), 1));
        Assert.NotNull(_users.Find(1));
    }

    [Fact]
    public async Task GetAll_FiltersByRoleAndStatus()
    {
        var service = CreateService();
        await service.SetActiveAsync(SuperAdminActor, 3, false);

        var admins = await service.GetAllAsync(null, UserRole.Admin, null, 100);
        var inactive = await service.GetAllAsync(null, null, false, 100);
        var found = await service.GetAllAsync("ulvi", null, null, 100);

        Assert.Equal(new[] { 2 }, admins.Select(u => u.Id));
        Assert.Equal(new[] { 3 }, inactive.Select(u => u.Id));
        Assert.Equal(new[] { 3 }, found.Select(u => u.Id));
    }

    [Fact]
    public async Task AuditLog_KeepsEntriesToOneCleanLineWithinTheColumns()
    {
        var audit = new AuditLogService(_logs, new ListLogger<AuditLogService>());

        await audit.RecordAsync(new AuditActor(7, new string('r', 40)), AuditActions.AccessDenied, "Endpoint", "GET /api/admin/users\r\nFAKE", new string('d', 900));

        var entry = Assert.Single(_logs.Entries);
        Assert.Equal(20, entry.ActorRole!.Length);
        Assert.Equal(500, entry.Details!.Length);
        Assert.DoesNotContain('\n', entry.TargetId!);
        Assert.DoesNotContain('\r', entry.TargetId!);
    }
}

public class UserStateCacheTests
{
    private readonly AdjustableTimeProvider _clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

    private UserStateCache Create(int seconds = 30) => new(Options.Create(new AuthSettings { UserStateCacheSeconds = seconds }), _clock);

    [Fact]
    public void RemembersAnAccountUntilItExpires()
    {
        var cache = Create();
        cache.Set(5, new UserState(UserRole.Admin, true));

        Assert.True(cache.TryGet(5, out var state));
        Assert.Equal(UserRole.Admin, state!.Role);

        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False(cache.TryGet(5, out _));
    }

    [Fact]
    public void RemembersThatAnAccountDoesNotExist()
    {
        var cache = Create();
        cache.Set(9, null);

        Assert.True(cache.TryGet(9, out var state));
        Assert.Null(state);
    }

    [Fact]
    public void Invalidate_ForgetsAtOnce()
    {
        var cache = Create();
        cache.Set(5, new UserState(UserRole.User, true));

        cache.Invalidate(5);

        Assert.False(cache.TryGet(5, out _));
    }

    [Fact]
    public void ZeroSeconds_RemembersNothing()
    {
        var cache = Create(0);
        cache.Set(5, new UserState(UserRole.User, true));

        Assert.False(cache.TryGet(5, out _));
    }
}
