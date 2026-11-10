using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Deleting an account over HTTP: a link is e-mailed first and nothing is deleted until the page of that link confirms; the personal
/// data goes, the bookings and payments stay, the sign-in stops, and the address can register again. No mail leaves the machine.
/// </summary>
public class AccountDeletionEndpointTests : IClassFixture<ApiFactory>
{
    private const string Password = "secret1";

    private readonly ApiFactory _factory;

    public AccountDeletionEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = true;
        _factory.Emails.Fail = false;
    }

    private async Task<(HttpClient Client, string Email, int UserId)> NewAccountAsync()
    {
        var email = UniqueEmail("delete");

        // Sign-in waits for a confirmed address while mail works, so the account is made while it does not (as the other API tests do).
        _factory.Emails.IsConfigured = false;
        AuthResponseDto account;
        try
        {
            account = await _factory.CreateClient().RegisterAsync(email, Password);
        }
        finally
        {
            _factory.Emails.IsConfigured = true;
        }

        return (_factory.ClientWith(account.AccessToken), email, account.User.Id);
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string password = Password)
        => client.PostAsJsonAsync("/api/account-deletion/request", new AccountDeletionRequestDto { Password = password, Language = "az" }, Json);

    private Task<HttpResponseMessage> CheckAsync(string token)
        => _factory.CreateClient().PostAsJsonAsync("/api/account-deletion/check", new ConfirmAccountDeletionRequestDto { Token = token }, Json);

    private Task<HttpResponseMessage> ConfirmAsync(string token)
        => _factory.CreateClient().PostAsJsonAsync("/api/account-deletion/confirm", new ConfirmAccountDeletionRequestDto { Token = token, Language = "az" }, Json);

    private static async Task<string> CodeOfAsync(HttpResponseMessage response) => (await response.ReadJsonAsync()).GetProperty("code").GetString()!;

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<(HttpClient Client, string Email, int UserId, string Token)> WithLinkAsync()
    {
        var (client, email, id) = await NewAccountAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await RequestAsync(client)).StatusCode);
        return (client, email, id, _factory.Emails.TokenFor(email));
    }

    [Fact]
    public async Task AWrongPassword_IsRefused_WithoutAnyMail()
    {
        var (client, email, _) = await NewAccountAsync();
        var before = _factory.Emails.Sent.Count(m => m.To == email);

        var response = await RequestAsync(client, "not-my-password");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("wrong_password", await CodeOfAsync(response));
        Assert.Equal(before, _factory.Emails.Sent.Count(m => m.To == email));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task TheRequest_OnlyMailsALink_TheAccountStaysAsItWas()
    {
        var (client, email, _) = await NewAccountAsync();

        var response = await RequestAsync(client);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var link = await response.ReadAsync<AccountDeletionLinkDto>();
        Assert.True(link.EmailSent);
        Assert.Equal(30, link.Minutes);
        Assert.DoesNotContain(email, link.MaskedEmail);

        var message = _factory.Emails.Sent.Last(m => m.To == email);
        Assert.Contains("https://aerova.test/account-delete-confirm.html?token=", message.TextBody);
        Assert.Contains("https://aerova.test/account-delete-confirm.html?token=", message.HtmlBody);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task OpeningTheLinkPage_Checks_ButDeletesNothing()
    {
        var (client, _, _, token) = await WithLinkAsync();

        var check = await CheckAsync(token);

        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CheckAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Confirming_ErasesThePersonalData_StopsTheSignIn_AndFreesTheAddress()
    {
        var (client, email, id, token) = await WithLinkAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profile/saved-passengers", new
        {
            firstName = "Aysun", lastName = "Panahli", dateOfBirth = "1990-03-01", gender = "Female", nationalityCode = "AZ"
        }, Json)).StatusCode);

        var confirm = await ConfirmAsync(token);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // The old sign-in token and the old password are dead.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // The row stays (bookings point at it) but holds nothing personal.
        var user = await InDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));
        Assert.False(user.IsActive);
        Assert.EndsWith("@deleted.invalid", user.Email);
        Assert.DoesNotContain("delete", user.FirstName, StringComparison.OrdinalIgnoreCase);
        Assert.Null(user.Phone);
        Assert.Null(user.DateOfBirth);
        Assert.Equal(0, await InDbAsync(db => db.SavedPassengers.CountAsync(p => p.UserId == id)));
        Assert.Equal(0, await InDbAsync(db => db.Notifications.CountAsync(n => n.UserId == id)));

        // The audit trail has the request and the deletion, and nothing secret.
        var actions = await InDbAsync(db => db.AuditLogs.AsNoTracking().Where(a => a.ActorUserId == id).ToListAsync());
        Assert.Contains(actions, a => a.Action == "AccountDeletionRequested");
        Assert.Contains(actions, a => a.Action == "AccountDeleted");
        Assert.All(actions, a => Assert.DoesNotContain(token, a.Details ?? string.Empty));
        Assert.All(actions, a => Assert.DoesNotContain(email, a.Details ?? string.Empty));

        // A notice goes to the old address, and the address can register again.
        Assert.Contains(_factory.Emails.Sent, m => m.To == email && m.Subject.Contains("silindi"));
        var again = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "Aysun", LastName = "Panahli", Email = email, Password = Password, ConfirmPassword = Password
        }, Json);
        Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheLink_WorksOnce_ARepeatSaysTheAccountIsDeleted()
    {
        var (_, _, _, token) = await WithLinkAsync();
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(token)).StatusCode);

        var repeat = await ConfirmAsync(token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeat.StatusCode);
        Assert.Equal("link_confirmed", await CodeOfAsync(repeat));
        Assert.Equal("link_confirmed", await CodeOfAsync(await CheckAsync(token)));
    }

    [Fact]
    public async Task AWrongOrExpiredToken_DeletesNothing()
    {
        var (client, email, id, token) = await WithLinkAsync();

        var wrong = await ConfirmAsync("this-is-not-a-real-token-at-all-0000000000");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal("link_invalid", await CodeOfAsync(wrong));
        Assert.Equal("link_invalid", await CodeOfAsync(await ConfirmAsync("short")));

        await InDbAsync(async db =>
        {
            foreach (var open in await db.EmailTokens.Where(t => t.UserId == id && t.Purpose == EmailTokenPurpose.DeleteAccount).ToListAsync())
            {
                open.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }

            return await db.SaveChangesAsync();
        });
        var expired = await ConfirmAsync(token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        Assert.Equal("link_expired", await CodeOfAsync(expired));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.True((await InDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id))).IsActive);
        Assert.Equal(email, (await InDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id))).Email);
    }

    [Fact]
    public async Task ANewRequest_ReplacesTheOlderLink()
    {
        var (client, email, id, first) = await WithLinkAsync();

        await InDbAsync(async db =>
        {
            foreach (var token in await db.EmailTokens.Where(t => t.UserId == id).ToListAsync())
            {
                token.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
            }

            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Accepted, (await RequestAsync(client)).StatusCode);
        var second = _factory.Emails.TokenFor(email);

        Assert.NotEqual(first, second);
        var old = await ConfirmAsync(first);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, old.StatusCode);
        Assert.Equal("link_used", await CodeOfAsync(old));
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(second)).StatusCode);
    }

    [Fact]
    public async Task APasswordReset_KillsAnOpenDeletionLink()
    {
        var (_, email, _, token) = await WithLinkAsync();

        Assert.Equal(HttpStatusCode.Accepted, (await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email }, Json)).StatusCode);
        var code = _factory.Emails.CodeFor(email);
        var verified = await _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-reset-code", new VerifyResetCodeRequestDto { Email = email, Code = code }, Json);
        var ticket = (await verified.ReadAsync<ResetTicketDto>()).Ticket;
        var done = await _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = ticket, NewPassword = "another-secret2", ConfirmPassword = "another-secret2" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);

        var response = await ConfirmAsync(token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("link_used", await CodeOfAsync(response));
    }

    [Fact]
    public async Task WithoutMail_NothingCanBeDeleted()
    {
        var (client, _, id) = await NewAccountAsync();
        _factory.Emails.IsConfigured = false;

        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RequestAsync(client)).StatusCode);
        }
        finally
        {
            _factory.Emails.IsConfigured = true;
        }

        Assert.Equal(0, await InDbAsync(db => db.EmailTokens.CountAsync(t => t.UserId == id && t.Purpose == EmailTokenPurpose.DeleteAccount)));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task StaffAccounts_CannotBeDeletedThisWay()
    {
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/account-deletion/request", new AccountDeletionRequestDto { Password = "anything" }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("account_not_deletable", await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task ARequest_NeedsSignIn()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/account-deletion/request", new AccountDeletionRequestDto { Password = Password }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AHeldBooking_BlocksTheDeletion_AndACancelledOneStaysAfterwards()
    {
        var seed = await _factory.GetSeedAsync();
        var (client, email, id) = await NewAccountAsync();
        var created = await client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11B"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.ReadAsync<BookingDto>();

        var refused = await RequestAsync(client);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("open_bookings", await CodeOfAsync(refused));

        // Once the hold is released the account can go; the booking stays, with its passengers, for the records.
        var cancelled = await client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new CancelBookingRequestDto { Pnr = booking.Pnr, LastName = "Panahli" }, Json);
        Assert.True(cancelled.IsSuccessStatusCode, await cancelled.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted, (await RequestAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(_factory.Emails.TokenFor(email))).StatusCode);

        var kept = await InDbAsync(db => db.Bookings.AsNoTracking().Include(b => b.Passengers).SingleAsync(b => b.Pnr == booking.Pnr));
        Assert.Equal(id, kept.UserId);
        Assert.Equal(BookingStatus.Cancelled, kept.Status);
        Assert.NotEmpty(kept.Passengers);
    }

    [Fact]
    public async Task TheRequestEndpoint_IsRateLimited()
    {
        var limited = new ApiFactory { RateLimitPerMinute = 3 };
        var client = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/account-deletion/confirm", new ConfirmAccountDeletionRequestDto { Token = "x" }, Json)).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
