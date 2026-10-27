using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A test API with the rules of the real one: only a signed-in account books, and only once its e-mail address is confirmed.</summary>
public sealed class ConfirmedAccountApiFactory : ApiFactory
{
    public ConfirmedAccountApiFactory()
    {
        RequireSignInToBook = true;
        RequireVerifiedEmailForBooking = true;
    }
}

/// <summary>
/// The address is confirmed once, when the account is made. After that a signed-in customer signs in again and again, books again and
/// again, and is never asked for a link or code on a booking. The e-mail sender is the test one: nothing leaves the machine.
/// </summary>
public class VerifiedBookingEndpointTests : IClassFixture<ConfirmedAccountApiFactory>
{
    private static int _document = 8000000;
    private readonly ConfirmedAccountApiFactory _factory;

    public VerifiedBookingEndpointTests(ConfirmedAccountApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = true;
        _factory.Emails.Fail = false;
    }

    private async Task<HttpResponseMessage> BookAsync(HttpClient client, string seat)
    {
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, seat);
        request.Passengers[0].DocumentNumber = $"C{Interlocked.Increment(ref _document)}";
        return await client.PostAsJsonAsync("/api/bookings", request, Json);
    }

    [Fact]
    public async Task ANewAccount_IsBlocked_UntilItsLinkIsOpened_AndThenBooksAgainAndAgainWithoutAnyMoreMail()
    {
        var anonymous = _factory.CreateClient();
        var email = UniqueEmail("confirmed");
        var account = await anonymous.RegisterAsync(email);
        var customer = _factory.ClientWith(account.AccessToken);

        // The sign-up mail went out; until its link is opened the booking is refused and nothing is held.
        Assert.Single(_factory.Emails.Sent, m => m.To == email);
        var blocked = await BookAsync(customer, "11A");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Contains("confirm your email", (await blocked.ReadJsonAsync()).GetProperty("detail").GetString());

        // Opening the link records the confirmation in the database.
        var token = _factory.Emails.TokenFor(email);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto { Token = token }, Json)).StatusCode);
        var mailsAfterConfirming = _factory.Emails.Sent.Count;

        // The same session books now, and so does every later session of the same account.
        Assert.Equal(HttpStatusCode.Created, (await BookAsync(customer, "11A")).StatusCode);

        for (var round = 0; round < 2; round++)
        {
            var signedIn = await (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = "secret1" }, Json)).ReadAsync<AuthResponseDto>();
            Assert.True(signedIn.User.EmailVerified);
            var again = _factory.ClientWith(signedIn.AccessToken);
            Assert.Equal(HttpStatusCode.Created, (await BookAsync(again, round == 0 ? "11B" : "11C")).StatusCode);
        }

        // Not one more e-mail was asked for by all that booking.
        Assert.Equal(mailsAfterConfirming, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task AGuest_StillCannotBook_AndTheAnswerIsNotAboutMail()
    {
        var seed = await _factory.GetSeedAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "1A"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
