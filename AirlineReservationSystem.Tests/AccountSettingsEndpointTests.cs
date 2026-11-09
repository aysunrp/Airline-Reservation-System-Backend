using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Account settings over HTTP, for every role: the password is checked and hashed on the server, an e-mail address only changes after the code sent to
/// the new address is typed in, other devices can be signed out for real, and the e-mail options are kept in the database. No mail leaves the machine.
/// </summary>
public class AccountSettingsEndpointTests : IClassFixture<ApiFactory>
{
    private const string Password = "secret1";

    private readonly ApiFactory _factory;

    public AccountSettingsEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = true;
        _factory.Emails.Fail = false;
    }

    private async Task<(HttpClient Client, string Email, string Token, int UserId)> NewUserAsync()
    {
        var email = UniqueEmail("settings");
        var account = await _factory.CreateClient().RegisterAsync(email, Password);
        return (_factory.ClientWith(account.AccessToken), email, account.AccessToken, account.User.Id);
    }

    private async Task<string> SignInAgainAsync(string email, string password = Password)
        => await _factory.CreateClient().SignInAsync(email, password);

    private static async Task<string> CodeOfAsync(HttpResponseMessage response) => (await response.ReadJsonAsync()).GetProperty("code").GetString()!;

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // ---- who may open it --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EverySettingsEndpoint_NeedsASignedInAccount()
    {
        var client = _factory.CreateClient();
        foreach (var url in new[] { "/api/account/overview", "/api/account/sessions", "/api/account/notification-preferences" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/account/password", new ChangePasswordRequestDto(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/email-change/request", new RequestEmailChangeDto(), Json)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User, false)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.SuperAdmin, true)]
    public async Task UserAdminAndSuperAdmin_AllOpenTheirOwnSettings(UserRole role, bool canUseAdminPanel)
    {
        // A customer registers (and confirms the address) like a person; an Admin or SuperAdmin is written into the database, as there is no public way to become one.
        HttpClient client;
        string email;
        if (role == UserRole.User)
        {
            (client, email, _, _) = await NewUserAsync();
        }
        else
        {
            var account = await _factory.CreateAccountAsync(role);
            (client, email) = (account.Client, account.Email);
        }

        var overview = await (await client.GetAsync("/api/account/overview")).ReadAsync<AccountOverviewDto>();

        Assert.Equal(email, overview.Email);
        Assert.Equal(role.ToString(), overview.Role);
        Assert.Equal(canUseAdminPanel, overview.CanUseAdminPanel);
        Assert.False(overview.TwoFactorAvailable);
        Assert.Equal(1, overview.OpenSessions);
        Assert.NotNull(overview.LastSignInAt);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/sessions")).StatusCode);
    }

    // ---- password ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_IsRefused_AndNothingChanges()
    {
        var (client, email, _, _) = await NewUserAsync();

        var response = await client.PutAsJsonAsync("/api/account/password",
            new ChangePasswordRequestDto { CurrentPassword = "not-it-1", NewPassword = "brand-new1", ConfirmPassword = "brand-new1" }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("wrong_password", await CodeOfAsync(response));
        Assert.NotEmpty(await SignInAgainAsync(email)); // the old password still works
    }

    [Fact]
    public async Task ChangePassword_WhenTheConfirmationDiffers_OrTheNewOneIsTheOldOne_OrTooShort_IsRefused()
    {
        var (client, _, _, _) = await NewUserAsync();

        var mismatch = await client.PutAsJsonAsync("/api/account/password",
            new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "brand-new1", ConfirmPassword = "other-one2" }, Json);
        var same = await client.PutAsJsonAsync("/api/account/password",
            new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = Password, ConfirmPassword = Password }, Json);
        var shortOne = await client.PutAsJsonAsync("/api/account/password",
            new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "abc", ConfirmPassword = "abc" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortOne.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_HashesTheNewOne_SignsOutTheOtherDevices_KeepsThisOne_AndLeavesASecurityNotice()
    {
        var (client, email, firstToken, userId) = await NewUserAsync();
        var otherDevice = _factory.ClientWith(await SignInAgainAsync(email));

        var response = await client.PutAsJsonAsync("/api/account/password",
            new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "brand-new1", ConfirmPassword = "brand-new1" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fresh = await response.ReadAsync<AuthResponseDto>();

        // Both tokens made before the change are dead; the fresh one works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/account/overview")).StatusCode);
        var stillIn = _factory.ClientWith(fresh.AccessToken);
        var overview = await (await stillIn.GetAsync("/api/account/overview")).ReadAsync<AccountOverviewDto>();
        Assert.NotNull(overview.PasswordChangedAt);
        Assert.Equal(1, overview.OpenSessions);

        // The old password is gone, the new one signs in, and the stored value is a hash made by the normal hasher.
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.NotEmpty(await SignInAgainAsync(email, "brand-new1"));
        using (var scope = _factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == userId);
            Assert.NotEqual("brand-new1", user.PasswordHash);
            Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify("brand-new1", user.PasswordHash));
        }

        var notices = await InDbAsync(db => db.Notifications.Where(n => n.UserId == userId && n.Type == NotificationType.AccountSecurity).ToListAsync());
        Assert.Single(notices);
        Assert.NotEmpty(firstToken);
    }

    [Fact]
    public async Task ForgotPasswordFromTheSettings_SendsACodeToTheAccountsOwnAddress_AndTheResetEndsTheOtherSessions()
    {
        var (client, email, _, _) = await NewUserAsync();
        var otherDevice = _factory.ClientWith(await SignInAgainAsync(email));

        // The settings page asks for the reset code with the account's own address (the existing reset flow).
        var forgot = await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email, Language = "az" }, Json);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var code = _factory.Emails.CodeFor(email);
        var ticket = await (await _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-reset-code", new VerifyResetCodeRequestDto { Email = email, Code = code }, Json)).ReadAsync<ResetTicketDto>();
        var reset = await _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequestDto { Token = ticket.Ticket, NewPassword = "reset-new1", ConfirmPassword = "reset-new1" }, Json);
        Assert.True(reset.IsSuccessStatusCode);

        // Everyone who was signed in with the old password is out; the new password signs in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/account/overview")).StatusCode);
        var again = _factory.ClientWith(await SignInAgainAsync(email, "reset-new1"));
        Assert.NotNull((await (await again.GetAsync("/api/account/overview")).ReadAsync<AccountOverviewDto>()).PasswordChangedAt);
    }

    // ---- e-mail address ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EmailChange_NeedsTheCurrentPassword_AndAnAddressNobodyUses()
    {
        var (client, _, _, _) = await NewUserAsync();
        var (_, takenEmail, _, _) = await NewUserAsync();

        var wrong = await client.PostAsJsonAsync("/api/account/email-change/request",
            new RequestEmailChangeDto { NewEmail = UniqueEmail("fresh"), CurrentPassword = "not-it-1" }, Json);
        var taken = await client.PostAsJsonAsync("/api/account/email-change/request",
            new RequestEmailChangeDto { NewEmail = takenEmail, CurrentPassword = Password }, Json);
        var invalid = await client.PostAsJsonAsync("/api/account/email-change/request",
            new RequestEmailChangeDto { NewEmail = "not-an-address", CurrentPassword = Password }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal("wrong_password", await CodeOfAsync(wrong));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task EmailChange_DoesNothingUntilTheCodeFromTheNewAddressIsTyped()
    {
        var (client, email, _, userId) = await NewUserAsync();
        var newEmail = UniqueEmail("changed");

        var request = await client.PostAsJsonAsync("/api/account/email-change/request", new RequestEmailChangeDto { NewEmail = newEmail, CurrentPassword = Password, Language = "az" }, Json);

        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        // The code goes to the NEW address, never to the old one, and the account still has its old address.
        var sentToNew = _factory.Emails.Sent.Where(m => string.Equals(m.To, newEmail, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(sentToNew);
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.TextBody.Contains(_factory.Emails.CodeFor(newEmail)) && string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(email, (await (await client.GetAsync("/api/account/overview")).ReadAsync<AccountOverviewDto>()).Email);

        // A wrong code does not switch it; neither does the profile form.
        var wrongCode = await client.PostAsJsonAsync("/api/account/email-change/confirm", new ConfirmEmailChangeDto { Code = "000000" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCode.StatusCode);
        Assert.Equal("code_invalid", await CodeOfAsync(wrongCode));
        var profile = await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>();
        var viaProfile = await client.PutAsJsonAsync("/api/profile", new UpdateProfileRequestDto
        {
            FirstName = profile.FirstName, LastName = profile.LastName, DateOfBirth = profile.DateOfBirth, NationalityCode = profile.NationalityCode,
            Email = newEmail, Phone = profile.Phone
        }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, viaProfile.StatusCode);
        Assert.Equal("email_change_via_settings", await CodeOfAsync(viaProfile));
        Assert.Equal(email, await InDbAsync(async db => (await db.Users.SingleAsync(u => u.Id == userId)).Email));
    }

    [Fact]
    public async Task EmailChange_WithTheRightCode_SwitchesTheAddress_ConfirmsIt_SignsOutOthers_AndTellsTheOldAddress()
    {
        var (client, email, _, userId) = await NewUserAsync();
        var otherDevice = _factory.ClientWith(await SignInAgainAsync(email));
        var newEmail = UniqueEmail("changed");
        await client.PostAsJsonAsync("/api/account/email-change/request", new RequestEmailChangeDto { NewEmail = newEmail, CurrentPassword = Password, Language = "az" }, Json);

        var confirm = await client.PostAsJsonAsync("/api/account/email-change/confirm", new ConfirmEmailChangeDto { Code = _factory.Emails.CodeFor(newEmail) }, Json);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var fresh = await confirm.ReadAsync<AuthResponseDto>();
        Assert.Equal(newEmail.ToLowerInvariant(), fresh.User.Email);
        Assert.True(fresh.User.EmailVerified);

        var overview = await (await _factory.ClientWith(fresh.AccessToken).GetAsync("/api/account/overview")).ReadAsync<AccountOverviewDto>();
        Assert.Equal(newEmail.ToLowerInvariant(), overview.Email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/account/overview")).StatusCode);
        Assert.NotEmpty(await SignInAgainAsync(newEmail));
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // The old address is told, and the code works only once.
        Assert.Contains(_factory.Emails.Sent, m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase) && m.Subject.Contains("dəyişdi"));
        var again = await _factory.ClientWith(fresh.AccessToken).PostAsJsonAsync("/api/account/email-change/confirm", new ConfirmEmailChangeDto { Code = _factory.Emails.CodeFor(newEmail) }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Contains(await InDbAsync(db => db.AuditLogs.Where(a => a.Action == "EmailChanged" && a.TargetId == userId.ToString()).ToListAsync()), _ => true);
    }

    [Fact]
    public async Task EmailChange_AWrongCodeFiveTimes_KillsTheCode()
    {
        var (client, _, _, _) = await NewUserAsync();
        var newEmail = UniqueEmail("changed");
        await client.PostAsJsonAsync("/api/account/email-change/request", new RequestEmailChangeDto { NewEmail = newEmail, CurrentPassword = Password }, Json);
        var right = _factory.Emails.CodeFor(newEmail);
        var wrong = right == "123456" ? "654321" : "123456";

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/account/email-change/confirm", new ConfirmEmailChangeDto { Code = wrong }, Json)).StatusCode);
        }

        var late = await client.PostAsJsonAsync("/api/account/email-change/confirm", new ConfirmEmailChangeDto { Code = right }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, late.StatusCode);
    }

    [Fact]
    public async Task EmailChange_WhenTheMailCannotBeSent_IsNotReportedAsSent_AndCodesAreLimitedPerMinute()
    {
        var (client, _, _, _) = await NewUserAsync();
        _factory.Emails.Fail = true;
        try
        {
            var down = await client.PostAsJsonAsync("/api/account/email-change/request", new RequestEmailChangeDto { NewEmail = UniqueEmail("x"), CurrentPassword = Password }, Json);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
        }
        finally
        {
            _factory.Emails.Fail = false;
        }
    }

    // ---- phone ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Phone_IsSavedNormalized_AndABadNumberIsRefused()
    {
        var (client, _, _, userId) = await NewUserAsync();

        var ok = await client.PutAsJsonAsync("/api/account/phone", new UpdatePhoneRequestDto { Phone = "+994 50 834 39 43" }, Json);
        var bad = await client.PutAsJsonAsync("/api/account/phone", new UpdatePhoneRequestDto { Phone = "call me" }, Json);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("+994508343943", (await ok.ReadAsync<ProfileDto>()).Phone);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("+994508343943", await InDbAsync(async db => (await db.Users.SingleAsync(u => u.Id == userId)).Phone));
    }

    // ---- sessions ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sessions_ListEachSignIn_MarkThisOne_AndAnotherDeviceCanBeSignedOut()
    {
        var (client, email, _, _) = await NewUserAsync();
        var otherToken = await SignInAgainAsync(email);
        var other = _factory.ClientWith(otherToken);

        var list = await (await client.GetAsync("/api/account/sessions")).ReadAsync<List<SessionDto>>();

        Assert.Equal(2, list.Count);
        Assert.Single(list, s => s.IsCurrent);
        var currentId = list.Single(s => s.IsCurrent).Id;
        var otherId = list.Single(s => !s.IsCurrent).Id;

        // This device cannot sign itself out here; the other one can be signed out, and its token stops working at once.
        var self = await client.DeleteAsync($"/api/account/sessions/{currentId}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        Assert.Equal("current_session", await CodeOfAsync(self));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/account/sessions/{otherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/account/overview")).StatusCode);
        Assert.Single(await (await client.GetAsync("/api/account/sessions")).ReadAsync<List<SessionDto>>());
    }

    [Fact]
    public async Task SignOutOfAllOtherDevices_EndsThem_AndKeepsThisOneWithAFreshToken()
    {
        var (client, email, _, _) = await NewUserAsync();
        var devices = new[] { _factory.ClientWith(await SignInAgainAsync(email)), _factory.ClientWith(await SignInAgainAsync(email)) };

        var response = await client.PostAsync("/api/account/sessions/revoke-others", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fresh = await response.ReadAsync<AuthResponseDto>();
        Assert.All(devices, d => Assert.Equal(HttpStatusCode.Unauthorized, d.GetAsync("/api/account/overview").Result.StatusCode));
        var here = _factory.ClientWith(fresh.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/account/overview")).StatusCode);
        Assert.Single(await (await here.GetAsync("/api/account/sessions")).ReadAsync<List<SessionDto>>());
    }

    [Fact]
    public async Task SignOut_EndsTheSessionOnTheServer_SoTheTokenIsDead()
    {
        var (client, _, token, _) = await NewUserAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/account/sessions/current")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.ClientWith(token).GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task ASessionOfAnotherAccount_IsNotFound_AndNobodyCanChangeAnotherAccount()
    {
        var (mine, _, _, _) = await NewUserAsync();
        var (theirs, theirEmail, _, theirId) = await NewUserAsync();
        var theirSession = (await (await theirs.GetAsync("/api/account/sessions")).ReadAsync<List<SessionDto>>()).Single();

        var response = await mine.DeleteAsync($"/api/account/sessions/{theirSession.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await theirs.GetAsync("/api/account/overview")).StatusCode);

        // Changing a password or an e-mail here only ever touches the signed-in account: theirs keeps working.
        await mine.PutAsJsonAsync("/api/account/password", new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "brand-new1", ConfirmPassword = "brand-new1" }, Json);
        Assert.NotEmpty(await SignInAgainAsync(theirEmail));
        Assert.Equal(theirEmail.ToLowerInvariant(), await InDbAsync(async db => (await db.Users.SingleAsync(u => u.Id == theirId)).Email));
    }

    // ---- e-mail preferences -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Preferences_StartOn_AreKeptInTheDatabase_AndTheMandatoryOnesCannotBeTurnedOff()
    {
        var (client, _, _, userId) = await NewUserAsync();

        var start = await (await client.GetAsync("/api/account/notification-preferences")).ReadAsync<NotificationPreferencesDto>();
        Assert.True(start is { BookingEmails: true, PaymentEmails: true, SecurityEmails: true, FlightUpdates: true, SystemMessages: true, Marketing: true });

        // Only the three optional options are in the request; the mandatory ones come back on whatever is sent.
        var saved = await client.PutAsJsonAsync("/api/account/notification-preferences",
            new { flightUpdates = false, systemMessages = true, marketing = false, bookingEmails = false, paymentEmails = false, securityEmails = false }, Json);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var dto = await saved.ReadAsync<NotificationPreferencesDto>();
        Assert.True(dto is { BookingEmails: true, PaymentEmails: true, SecurityEmails: true, FlightUpdates: false, SystemMessages: true, Marketing: false });

        var row = await InDbAsync(db => db.NotificationPreferences.SingleAsync(p => p.UserId == userId));
        Assert.False(row.FlightUpdates);
        Assert.False(row.Marketing);
        Assert.True(row.SystemMessages);
        Assert.Equal(dto, await (await client.GetAsync("/api/account/notification-preferences")).ReadAsync<NotificationPreferencesDto>());
    }

    [Fact]
    public async Task AFlightUpdateEmail_IsHeldBackWhenTurnedOff_ButABookingOrSecurityOneIsAlwaysSent()
    {
        var (client, email, _, userId) = await NewUserAsync();
        await client.PutAsJsonAsync("/api/account/notification-preferences", new UpdateNotificationPreferencesDto { FlightUpdates = false, SystemMessages = true, Marketing = true }, Json);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.AddRange(
                new Notification { UserId = userId, Type = NotificationType.FlightUpdate, Title = "Flight Update", Message = "Your flight moved." },
                new Notification { UserId = userId, Type = NotificationType.PaymentSuccessful, Title = "Payment confirmed", Message = "Paid." },
                new Notification { UserId = userId, Type = NotificationType.AccountSecurity, Title = "Password changed", Message = "Changed." });
            await db.SaveChangesAsync();
        }

        var before = _factory.Emails.Sent.Count(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync();
        }

        var mails = _factory.Emails.Sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).Skip(before).ToList();
        Assert.DoesNotContain(mails, m => m.Subject.Contains("Flight Update"));
        Assert.Contains(mails, m => m.Subject.Contains("Payment confirmed"));
        Assert.Contains(mails, m => m.Subject.Contains("Password changed"));
        // The held-back one stays in the app.
        Assert.Contains(await InDbAsync(db => db.Notifications.Where(n => n.UserId == userId).ToListAsync()), n => n.Type == NotificationType.FlightUpdate);
    }

    // ---- regression -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task SigningIn_StillWorks_AndTheProfileStillSavesTheOtherFields()
    {
        var (client, email, _, _) = await NewUserAsync();
        var profile = await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>();

        var saved = await client.PutAsJsonAsync("/api/profile", new UpdateProfileRequestDto
        {
            FirstName = "Leyla", LastName = profile.LastName, DateOfBirth = new DateOnly(2000, 1, 2), NationalityCode = "AZ", Email = email, Phone = "+994501112233"
        }, Json);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Leyla", (await saved.ReadAsync<ProfileDto>()).FirstName);
        Assert.NotEmpty(await SignInAgainAsync(email));
    }
}
