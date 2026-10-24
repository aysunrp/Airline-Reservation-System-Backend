using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Account;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Over HTTP with the fake e-mail sender (no real mail goes anywhere): register, confirm the address with the link, forget and
/// reset a password, and the answers that must not tell anyone which addresses have accounts.
/// </summary>
public class AuthEmailEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthEmailEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = true;
        _factory.Emails.Fail = false;
    }

    private static RegisterRequestDto Registration(string email, string? language = null) => new()
    {
        FirstName = "Aysun", LastName = "Panahli", Email = email, Password = "secret1", ConfirmPassword = "secret1", Language = language
    };

    private static async Task<HttpResponseMessage> SignIn(HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = password }, Json);

    [Fact]
    public async Task Register_EmailsALink_AndOpeningItConfirmsTheAddress_Once()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("verify");

        var registered = await client.PostAsJsonAsync("/api/auth/register", Registration(email, "az"), Json);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var body = await registered.ReadAsync<RegisterResponseDto>();
        Assert.True(body.VerificationEmailSent);
        Assert.False(body.User.EmailVerified);

        var message = _factory.Emails.Sent.Last(m => m.To == email);
        Assert.Contains("təsdiqləyin", message.Subject);
        Assert.Contains("https://aerova.test/verify-email.html?token=", message.TextBody);
        var token = _factory.Emails.TokenFor(email);

        // Signed in but not confirmed yet.
        Assert.False((await (await SignIn(client, email, "secret1")).ReadAsync<AuthResponseDto>()).User.EmailVerified);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = token }, Json)).StatusCode);
        Assert.True((await (await SignIn(client, email, "secret1")).ReadAsync<AuthResponseDto>()).User.EmailVerified);

        // The link works once.
        var again = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = token }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AWrongOrEmptyToken_IsRefused()
    {
        var client = _factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = "definitely-not-a-token" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal("This link is invalid or has expired.", (await wrong.ReadJsonAsync()).GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = "" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Register_WithoutSmtp_StillCreatesTheAccount_AndSaysNoMailWentOut()
    {
        _factory.Emails.IsConfigured = false;
        var client = _factory.CreateClient();
        var email = UniqueEmail("nosmtp");

        var registered = await client.PostAsJsonAsync("/api/auth/register", Registration(email), Json);

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var body = await registered.ReadAsync<RegisterResponseDto>();
        Assert.False(body.VerificationEmailSent);
        Assert.False(body.User.EmailVerified);
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == email);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(client, email, "secret1")).StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_Returns202ForAnyAddress_AndMailsOnlyAnExistingAccount()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("forgot");
        await client.RegisterAsync(email);
        var unknown = UniqueEmail("nobody");
        var before = _factory.Emails.Sent.Count;

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email }, Json);
        var stranger = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = unknown }, Json);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, stranger.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await stranger.Content.ReadAsStringAsync());
        Assert.Equal(before + 1, _factory.Emails.Sent.Count);
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == unknown);
        Assert.Contains("reset-password.html?token=", _factory.Emails.Sent.Last(m => m.To == email).TextBody);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = "not-an-email" }, Json)).StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithoutSmtp_Returns503_ForEveryAddress()
    {
        _factory.Emails.IsConfigured = false;
        var client = _factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = UniqueEmail() }, Json);
        var other = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = UniqueEmail() }, Json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, known.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, other.StatusCode);
        Assert.Equal((await known.ReadJsonAsync()).GetProperty("detail").GetString(), (await other.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ResetPassword_ChoosesANewPassword_Once()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("reset");
        await client.RegisterAsync(email);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email }, Json);
        var token = _factory.Emails.TokenFor(email);

        var mismatch = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = "brand-new1", ConfirmPassword = "other-one2" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = "brand-new1", ConfirmPassword = "brand-new1" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SignIn(client, email, "brand-new1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignIn(client, email, "secret1")).StatusCode);

        var reused = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = "third-pass3", ConfirmPassword = "third-pass3" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact]
    public async Task Resend_NeedsSignIn_SendsAnotherLink_AndStopsOnceConfirmed()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/resend-verification", new ResendVerificationRequestDto(), Json)).StatusCode);

        var email = UniqueEmail("resend");
        var account = await client.RegisterAsync(email);
        var signedIn = _factory.CreateClient().WithToken(account.AccessToken);

        // A moment after registering, a second e-mail is held back.
        var tooSoon = await signedIn.PostAsJsonAsync("/api/auth/resend-verification", new ResendVerificationRequestDto(), Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
        Assert.True(int.Parse(tooSoon.Headers.GetValues("Retry-After").Single()) > 0);

        await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = _factory.Emails.TokenFor(email) }, Json);
        Assert.Equal(HttpStatusCode.Conflict, (await signedIn.PostAsJsonAsync("/api/auth/resend-verification", new ResendVerificationRequestDto(), Json)).StatusCode);
    }

    [Fact]
    public async Task AFailedDelivery_NeverRevealsAnAccount_NorBreaksRegistration()
    {
        _factory.Emails.Fail = true;
        var client = _factory.CreateClient();
        var email = UniqueEmail("fail");

        var registered = await client.PostAsJsonAsync("/api/auth/register", Registration(email), Json);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.False((await registered.ReadAsync<RegisterResponseDto>()).VerificationEmailSent);

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email }, Json);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
    }

    [Fact]
    public async Task TheAdminSeederAccount_CountsAsVerified_AndNoSignUpCanMakeAnAdmin()
    {
        var admin = await _factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/dashboard")).StatusCode);

        var client = _factory.CreateClient();
        var token = (await client.RegisterAsync(UniqueEmail("customer"))).AccessToken;
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClient().WithToken(token).GetAsync("/api/admin/dashboard")).StatusCode);
    }
}
