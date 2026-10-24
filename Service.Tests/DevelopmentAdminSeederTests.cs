using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Seed;
using Service.Services;

namespace Service.Tests;

public class DevelopmentAdminSeederTests
{
    // Generated per run; these are test values, not real credentials.
    private readonly string _email = $"admin.{Guid.NewGuid():N}@example.com";
    private readonly string _password = $"Dev{Guid.NewGuid():N}"[..20];

    private readonly FakeUserRepository _users = new();
    private readonly PasswordHasher _hasher = new();

    private DevelopmentAdminSeeder CreateSeeder() => new(_users, _hasher);

    [Fact]
    public async Task NewEmail_CreatesAnAdminWithAHashedPassword()
    {
        var result = await CreateSeeder().EnsureAdminAsync($"  {_email.ToUpperInvariant()} ", _password);

        Assert.Equal(DevelopmentAdminOutcome.Created, result.Outcome);
        var user = Assert.Single(await _users.GetAllAsync());
        Assert.Equal((_email, UserRole.Admin, true), (user.Email, user.Role, user.IsActive));
        // The trusted seeder needs no e-mail link: the admin counts as verified.
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotEqual(_password, user.PasswordHash);
        Assert.DoesNotContain(_password, user.PasswordHash);
        Assert.True(_hasher.Verify(_password, user.PasswordHash));
    }

    [Fact]
    public async Task RunningAgain_KeepsTheAccountAndItsPassword()
    {
        await CreateSeeder().EnsureAdminAsync(_email, _password);
        var hash = (await _users.GetAllAsync()).Single().PasswordHash;

        var again = await CreateSeeder().EnsureAdminAsync(_email, _password + "9x");

        Assert.Equal(DevelopmentAdminOutcome.AlreadyAdmin, again.Outcome);
        var user = Assert.Single(await _users.GetAllAsync());
        Assert.Equal(hash, user.PasswordHash);
    }

    [Fact]
    public async Task ExistingClientAccount_IsNeitherChangedNorPromoted()
    {
        var client = new User { Email = _email, PasswordHash = _hasher.Hash("client-pass1"), FirstName = "Leyla", LastName = "Mammadova", Role = UserRole.Client };
        await _users.AddAsync(client);

        var result = await CreateSeeder().EnsureAdminAsync(_email, _password);

        Assert.Equal(DevelopmentAdminOutcome.ExistingNonAdmin, result.Outcome);
        var user = Assert.Single(await _users.GetAllAsync());
        Assert.Equal((UserRole.Client, "Leyla"), (user.Role, user.FirstName));
        Assert.True(_hasher.Verify("client-pass1", user.PasswordHash));
    }

    [Theory]
    [InlineData(null, "Valid123pass", "Seed:AdminEmail is not set.")]
    [InlineData("   ", "Valid123pass", "Seed:AdminEmail is not set.")]
    [InlineData("not-an-email", "Valid123pass", "Seed:AdminEmail is not a valid email address.")]
    [InlineData("admin@example.com", null, "Seed:AdminPassword is not set.")]
    [InlineData("admin@example.com", "short1", "Seed:AdminPassword must be")]
    [InlineData("admin@example.com", "onlyletters", "Seed:AdminPassword must be")]
    [InlineData("admin@example.com", "1234567890", "Seed:AdminPassword must be")]
    public async Task InvalidConfiguration_StoresNothing_AndNamesOnlyTheSetting(string? email, string? password, string problem)
    {
        var result = await CreateSeeder().EnsureAdminAsync(email, password);

        Assert.Equal(DevelopmentAdminOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains(result.Problems, p => p.StartsWith(problem, StringComparison.Ordinal));
        Assert.All(result.Problems, p =>
        {
            if (!string.IsNullOrWhiteSpace(email)) Assert.DoesNotContain(email.Trim(), p);
            if (!string.IsNullOrEmpty(password)) Assert.DoesNotContain(password, p);
        });
        Assert.Empty(await _users.GetAllAsync());
    }
}
