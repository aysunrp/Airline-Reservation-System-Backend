using Domain.Entities;
using Domain.Enums;
using Service.Helpers;
using Service.Helpers.DTOs.Seed;
using Service.Services;

namespace Service.Tests;

public class SuperAdminProvisioningServiceTests
{
    // Generated per run; these are test values, not real credentials.
    private readonly string _email = $"super.{Guid.NewGuid():N}@example.com";
    private readonly string _password = $"Sup{Guid.NewGuid():N}"[..24];

    private static readonly DateTime Verified = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeAuditLogRepository _logs = new();
    private readonly PasswordHasher _hasher = new();
    private readonly FakeUserRepository _users = new();

    private SuperAdminProvisioningService CreateService()
        => new(_users, _hasher, new AuditLogService(_logs, new ListLogger<AuditLogService>()), _clock);

    private async Task<User> AddAsync(string email, UserRole role, bool active = true, DateTime? verifiedAt = null)
    {
        var user = new User { Email = email, PasswordHash = _hasher.Hash("old-pass-123"), FirstName = "A", LastName = "B", Role = role, IsActive = active, EmailVerifiedAt = verifiedAt };
        await _users.AddAsync(user);
        return user;
    }

    [Fact]
    public async Task Bootstrap_CreatesAConfirmedSuperAdminWithAHashedPassword_AndLogsIt()
    {
        var result = await CreateService().BootstrapAsync($"  {_email.ToUpperInvariant()} ", _password);

        Assert.Equal(SuperAdminBootstrapOutcome.Created, result.Outcome);
        var user = Assert.Single(await _users.GetAllAsync());
        Assert.Equal((_email, UserRole.SuperAdmin, true), (user.Email, user.Role, user.IsActive));
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.DoesNotContain(_password, user.PasswordHash);
        Assert.True(_hasher.Verify(_password, user.PasswordHash));

        var entry = Assert.Single(_logs.Entries);
        Assert.Equal((AuditActions.SuperAdminBootstrapped, "User", user.Id.ToString(), (int?)null, "Server"), (entry.Action, entry.TargetType, entry.TargetId, entry.ActorUserId, entry.ActorRole));
        Assert.DoesNotContain(_password, entry.Details ?? string.Empty);
    }

    [Fact]
    public async Task Bootstrap_WithAnActiveSuperAdmin_DoesNothing_EvenForAnotherAddress()
    {
        await AddAsync("first@example.com", UserRole.SuperAdmin, verifiedAt: Verified);

        var result = await CreateService().BootstrapAsync(_email, _password);

        Assert.Equal(SuperAdminBootstrapOutcome.AlreadyExists, result.Outcome);
        Assert.Single(await _users.GetAllAsync());
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task Bootstrap_WithOnlyADeactivatedSuperAdmin_CreatesANewOne()
    {
        await AddAsync("gone@example.com", UserRole.SuperAdmin, active: false, verifiedAt: Verified);

        var result = await CreateService().BootstrapAsync(_email, _password);

        Assert.Equal(SuperAdminBootstrapOutcome.Created, result.Outcome);
        Assert.Equal(2, (await _users.GetAllAsync()).Count);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Bootstrap_NeverChangesOrPromotesAnExistingAccount(UserRole role)
    {
        var existing = await AddAsync(_email, role, verifiedAt: Verified);
        var hash = existing.PasswordHash;

        var result = await CreateService().BootstrapAsync(_email.ToUpperInvariant(), _password);

        Assert.Equal(SuperAdminBootstrapOutcome.EmailTaken, result.Outcome);
        Assert.Equal((role, hash), (existing.Role, existing.PasswordHash));
        Assert.Single(await _users.GetAllAsync());
    }

    [Theory]
    [InlineData(null, "ValidPassw0rd123")]
    [InlineData("not-an-email", "ValidPassw0rd123")]
    [InlineData("a@example.com", null)]
    [InlineData("a@example.com", "Short1")]
    [InlineData("a@example.com", "NoDigitsAtAllHere")]
    [InlineData("a@example.com", "1234567890123456")]
    public async Task Bootstrap_InvalidConfiguration_StoresNothing_AndNamesOnlyTheKeys(string? email, string? password)
    {
        var result = await CreateService().BootstrapAsync(email, password);

        Assert.Equal(SuperAdminBootstrapOutcome.InvalidConfiguration, result.Outcome);
        Assert.NotEmpty(result.Problems);
        Assert.All(result.Problems, p => Assert.StartsWith("Bootstrap:SuperAdmin", p));
        if (password is not null)
        {
            Assert.All(result.Problems, p => Assert.DoesNotContain(password, p));
        }

        Assert.Empty(await _users.GetAllAsync());
    }

    [Fact]
    public async Task Promote_AnActiveConfirmedAdmin_KeepsThePassword_AndLogsIt()
    {
        var admin = await AddAsync(_email, UserRole.Admin, verifiedAt: Verified);
        var hash = admin.PasswordHash;

        var result = await CreateService().PromoteAsync($" {_email.ToUpperInvariant()} ");

        Assert.Equal(SuperAdminPromotionOutcome.Promoted, result.Outcome);
        Assert.Equal((UserRole.SuperAdmin, hash), (admin.Role, admin.PasswordHash));
        var entry = Assert.Single(_logs.Entries);
        Assert.Equal((AuditActions.UserRoleChanged, "Server", "Admin -> SuperAdmin (server command)"), (entry.Action, entry.ActorRole, entry.Details));
    }

    [Fact]
    public async Task Promote_AUser_IsRefused_BecauseAnAdminComesFirst()
    {
        var user = await AddAsync(_email, UserRole.User, verifiedAt: Verified);

        var result = await CreateService().PromoteAsync(_email);

        Assert.Equal(SuperAdminPromotionOutcome.NotAnAdmin, result.Outcome);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task Promote_AnUnconfirmedAdmin_IsRefused()
    {
        var admin = await AddAsync(_email, UserRole.Admin, verifiedAt: null);

        var result = await CreateService().PromoteAsync(_email);

        Assert.Equal(SuperAdminPromotionOutcome.EmailNotVerified, result.Outcome);
        Assert.Equal(UserRole.Admin, admin.Role);
    }

    [Fact]
    public async Task Promote_ADeactivatedAdmin_IsRefused()
    {
        var admin = await AddAsync(_email, UserRole.Admin, active: false, verifiedAt: Verified);

        var result = await CreateService().PromoteAsync(_email);

        Assert.Equal(SuperAdminPromotionOutcome.Inactive, result.Outcome);
        Assert.Equal(UserRole.Admin, admin.Role);
    }

    [Fact]
    public async Task Promote_OtherCases()
    {
        await AddAsync("already@example.com", UserRole.SuperAdmin, verifiedAt: Verified);
        var service = CreateService();

        Assert.Equal(SuperAdminPromotionOutcome.AlreadySuperAdmin, (await service.PromoteAsync("already@example.com")).Outcome);
        Assert.Equal(SuperAdminPromotionOutcome.NotFound, (await service.PromoteAsync("nobody@example.com")).Outcome);
        Assert.Equal(SuperAdminPromotionOutcome.InvalidEmail, (await service.PromoteAsync("not-an-email")).Outcome);
        Assert.Equal(SuperAdminPromotionOutcome.InvalidEmail, (await service.PromoteAsync(null)).Outcome);
        Assert.Empty(_logs.Entries);
    }
}
