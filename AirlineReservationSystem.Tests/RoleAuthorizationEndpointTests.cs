using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// User, Admin and SuperAdmin over HTTP: who may reach what, that a changed role or a deactivated account stops
/// working with the next request, and that account management leaves a trail in the audit log.
/// </summary>
public class RoleAuthorizationEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RoleAuthorizationEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string RoleUrl(int id) => $"/api/admin/users/{id}/role";

    private static string ActiveUrl(int id) => $"/api/admin/users/{id}/active";

    private async Task<List<(string Action, string? TargetId, string? Details)>> AuditOfAsync(int actorUserId, string action)
    {
        using var scope = _factory.Services.CreateScope();
        var entries = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .Where(a => a.ActorUserId == actorUserId && a.Action == action)
            .OrderBy(a => a.Id)
            .ToListAsync();
        return entries.Select(a => (a.Action, a.TargetId, a.Details)).ToList();
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized_EverywhereInTheAdminArea()
    {
        var anonymous = _factory.CreateClient();

        foreach (var url in new[] { "/api/admin/dashboard", "/api/admin/flights", "/api/admin/users", "/api/admin/audit-logs" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/users", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task User_IsForbidden_InTheAdminArea_AndTheAttemptIsLogged()
    {
        var account = await _factory.CreateAccountAsync(UserRole.User);

        foreach (var url in new[] { "/api/admin/dashboard", "/api/admin/flights", "/api/admin/bookings", "/api/admin/users", "/api/admin/audit-logs" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await account.Client.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await account.Client.PutAsJsonAsync(RoleUrl(account.Id), new { role = "Admin" }, Json)).StatusCode);

        var denied = await AuditOfAsync(account.Id, AuditActions.AccessDenied);
        Assert.Equal(6, denied.Count);
        Assert.Contains(denied, d => d.TargetId == "GET /api/admin/users");
    }

    [Fact]
    public async Task Admin_UsesTheAdminArea_ButNotTheSuperAdminOnes()
    {
        var admin = await _factory.CreateAccountAsync(UserRole.Admin);

        foreach (var url in new[] { "/api/admin/dashboard", "/api/admin/flights", "/api/admin/bookings", "/api/admin/cancellation-rules" })
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.Client.GetAsync(url)).StatusCode);
        }

        // Accounts, roles and the audit log.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync("/api/admin/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PostAsJsonAsync("/api/admin/users", NewAdminBody(UniqueEmail("x")), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PutAsJsonAsync(RoleUrl(admin.Id), new { role = "User" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync($"/api/admin/users/{admin.Id}")).StatusCode);

        // Destructive actions and generation.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/flights/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/routes/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/airports/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/aircraft/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/promo-codes/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/schedules/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PostAsync("/api/admin/schedules/generate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PostAsync("/api/admin/schedules/1/generate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/booking-options/meals/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/booking-options/baggage/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PostAsJsonAsync("/api/admin/cancellation-rules", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PutAsJsonAsync("/api/admin/cancellation-rules/1", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.DeleteAsync("/api/admin/cancellation-rules/999999")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_ReachesBothAreas()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);

        foreach (var url in new[] { "/api/admin/dashboard", "/api/admin/flights", "/api/admin/cancellation-rules", "/api/admin/users", "/api/admin/audit-logs" })
        {
            Assert.Equal(HttpStatusCode.OK, (await superAdmin.Client.GetAsync(url)).StatusCode);
        }

        // The SuperAdmin-only actions get past the policy; this flight just does not exist.
        Assert.Equal(HttpStatusCode.NotFound, (await superAdmin.Client.DeleteAsync("/api/admin/flights/999999")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CreatesAnAdmin_WhoSignsInAndWorks_AndItIsLogged()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var email = UniqueEmail("newadmin");

        var created = await superAdmin.Client.PostAsJsonAsync("/api/admin/users", NewAdminBody(email), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var admin = await created.ReadAsync<AdminUserDto>();
        Assert.Equal(("Admin", true, true), (admin.Role, admin.IsActive, admin.EmailVerified));

        var token = await _factory.CreateClient().SignInAsync(email, "Passw0rd99");
        var adminClient = _factory.ClientWith(token);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/admin/users")).StatusCode);

        var logged = Assert.Single(await AuditOfAsync(superAdmin.Id, AuditActions.UserCreated));
        Assert.Equal(admin.Id.ToString(), logged.TargetId);

        // The same address twice, and a weak password.
        Assert.Equal(HttpStatusCode.Conflict, (await superAdmin.Client.PostAsJsonAsync("/api/admin/users", NewAdminBody(email.ToUpperInvariant()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.Client.PostAsJsonAsync("/api/admin/users", NewAdminBody(UniqueEmail("weak"), "short"), Json)).StatusCode);
    }

    [Fact]
    public async Task GrantingAndTakingBackAdmin_WorksAtOnce_ForTokensThatAreAlreadyOut()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var registered = await _factory.CreateClient().RegisterAsync();
        var userId = registered.User.Id;
        var userClient = _factory.ClientWith(registered.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/profile")).StatusCode);

        var granted = await superAdmin.Client.PutAsJsonAsync(RoleUrl(userId), new { role = "Admin" }, Json);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal("Admin", (await granted.ReadAsync<AdminUserDto>()).Role);

        // The token says User, the account is an Admin now: it is not accepted any more, so nobody keeps an out-of-date role.
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/profile")).StatusCode);

        var adminToken = await _factory.CreateClient().SignInAsync(registered.User.Email, "secret1");
        var adminClient = _factory.ClientWith(adminToken);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/admin/dashboard")).StatusCode);

        var revoked = await superAdmin.Client.PutAsJsonAsync(RoleUrl(userId), new { role = "User" }, Json);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminClient.GetAsync("/api/admin/dashboard")).StatusCode);

        var log = await AuditOfAsync(superAdmin.Id, AuditActions.UserRoleChanged);
        Assert.Equal(new[] { "User -> Admin", "Admin -> User" }, log.Select(l => l.Details));
    }

    [Fact]
    public async Task Deactivating_StopsTheTokenAndTheSignIn_AtOnce()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var registered = await _factory.CreateClient().RegisterAsync();
        var userClient = _factory.ClientWith(registered.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/profile")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await superAdmin.Client.PutAsJsonAsync(ActiveUrl(registered.User.Id), new { isActive = false }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/profile")).StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = registered.User.Email, Password = "secret1" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await superAdmin.Client.PutAsJsonAsync(ActiveUrl(registered.User.Id), new { isActive = true }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = registered.User.Email, Password = "secret1" }, Json)).StatusCode);

        var actions = new[] { AuditActions.UserDeactivated, AuditActions.UserActivated };
        foreach (var action in actions)
        {
            Assert.Single(await AuditOfAsync(superAdmin.Id, action));
        }
    }

    [Fact]
    public async Task SuperAdminRole_CannotBeGivenOrChanged_AndYouCannotDeactivateOrDeleteYourself()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var other = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var registered = await _factory.CreateClient().RegisterAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await superAdmin.Client.PutAsJsonAsync(RoleUrl(registered.User.Id), new { role = "SuperAdmin" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await superAdmin.Client.PutAsJsonAsync(RoleUrl(other.Id), new { role = "User" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await superAdmin.Client.PutAsJsonAsync(ActiveUrl(superAdmin.Id), new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await superAdmin.Client.DeleteAsync($"/api/admin/users/{superAdmin.Id}")).StatusCode);

        // Still a SuperAdmin, and still signed in.
        Assert.Equal(HttpStatusCode.OK, (await superAdmin.Client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await superAdmin.Client.PutAsJsonAsync(RoleUrl(999999), new { role = "Admin" }, Json)).StatusCode);
    }

    [Fact]
    public async Task DeletingAnAccount_StopsItsToken_AndIsLogged()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var registered = await _factory.CreateClient().RegisterAsync();
        var userClient = _factory.ClientWith(registered.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await superAdmin.Client.DeleteAsync($"/api/admin/users/{registered.User.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(registered.User.Id.ToString(), Assert.Single(await AuditOfAsync(superAdmin.Id, AuditActions.UserDeleted)).TargetId);
    }

    [Fact]
    public async Task Users_ListFiltersByRole_AndNeverShowsPasswords()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var admin = await _factory.CreateAccountAsync(UserRole.Admin);

        var response = await superAdmin.Client.GetAsync("/api/admin/users?role=Admin&take=500");
        var users = await response.ReadAsync<List<AdminUserDto>>();

        Assert.Contains(users, u => u.Id == admin.Id);
        Assert.All(users, u => Assert.Equal("Admin", u.Role));
        var raw = await (await superAdmin.Client.GetAsync("/api/admin/users")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditLog_ListsEntries_AndFiltersByAction()
    {
        var superAdmin = await _factory.CreateAccountAsync(UserRole.SuperAdmin);
        var registered = await _factory.CreateClient().RegisterAsync();
        await superAdmin.Client.PutAsJsonAsync(RoleUrl(registered.User.Id), new { role = "Admin" }, Json);

        var entries = await (await superAdmin.Client.GetAsync($"/api/admin/audit-logs?action={AuditActions.UserRoleChanged}&actorUserId={superAdmin.Id}")).ReadAsync<List<AuditLogDto>>();

        var entry = Assert.Single(entries);
        Assert.Equal((superAdmin.Id, "SuperAdmin", "User", registered.User.Id.ToString(), "User -> Admin"), (entry.ActorUserId, entry.ActorRole, entry.TargetType, entry.TargetId, entry.Details));
    }

    [Fact]
    public async Task Profile_Update_CannotChangeTheRole()
    {
        var registered = await _factory.CreateClient().RegisterAsync();
        var client = _factory.ClientWith(registered.AccessToken);

        var response = await client.PutAsJsonAsync("/api/profile", new
        {
            title = "Ms", firstName = "Aysun", lastName = "Panahli", dateOfBirth = "2004-06-16", nationalityCode = "AZ",
            email = registered.User.Email, phone = "+994508343943", role = "SuperAdmin", isAdmin = true
        }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = registered.User.Email, Password = "secret1" }, Json);
        Assert.Equal("User", (await login.ReadAsync<AuthResponseDto>()).User.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task ChangingTheEmail_MeansConfirmingAgain_AndTheOldLinkIsDead()
    {
        var registered = await _factory.CreateClient().RegisterAsync();
        var userId = registered.User.Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(u => u.Id == userId)).EmailVerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var client = _factory.ClientWith(registered.AccessToken);
        var newEmail = UniqueEmail("moved");
        var update = await client.PutAsJsonAsync("/api/profile", new
        {
            title = "Ms", firstName = "Aysun", lastName = "Panahli", dateOfBirth = "2004-06-16", nationalityCode = "AZ", email = newEmail, phone = "+994508343943"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = newEmail, Password = "secret1" }, Json);
        Assert.False((await login.ReadAsync<AuthResponseDto>()).User.EmailVerified);
        Assert.Contains(_factory.Emails.Sent, m => string.Equals(m.To, newEmail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PromoAndBookingLookups_AreLimitedPerIpAddress()
    {
        var limited = new ApiFactory { LookupRateLimitPerMinute = 3 };
        await limited.InitializeAsync();

        try
        {
            var client = limited.CreateClient();
            for (var i = 0; i < 3; i++)
            {
                Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/promo-codes/NOSUCH")).StatusCode);
            }

            var refused = await client.GetAsync("/api/promo-codes/NOSUCH");
            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
            Assert.Equal("60", refused.Headers.GetValues("Retry-After").Single());

            // The booking look-up shares the limit; searching flights does not.
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/bookings/ABCDEF?lastName=Smith")).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/airports")).StatusCode);
        }
        finally
        {
            await ((IAsyncLifetime)limited).DisposeAsync();
        }
    }

    private static object NewAdminBody(string email, string password = "Passw0rd99")
        => new { email, firstName = "New", lastName = "Admin", password };
}
