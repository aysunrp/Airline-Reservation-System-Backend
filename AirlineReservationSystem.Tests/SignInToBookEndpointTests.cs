using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A test API where booking needs a signed-in account, as in the real one.</summary>
public sealed class SignInApiFactory : ApiFactory
{
    public SignInApiFactory()
    {
        RequireSignInToBook = true;
    }
}

public class SignInToBookEndpointTests : IClassFixture<SignInApiFactory>
{
    private readonly SignInApiFactory _factory;

    public SignInToBookEndpointTests(SignInApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WithoutAToken_BookingIsRefused_AndTheSeatStaysFree()
    {
        var seed = await _factory.GetSeedAsync();
        var guest = _factory.CreateClient();

        var refused = await guest.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // The same seat can still be booked by an account: the refused request held nothing.
        var account = _factory.ClientWith((await _factory.CreateClient().RegisterAsync()).AccessToken);
        var created = await account.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.ReadAsync<BookingDto>();
        Assert.Equal(Domain.Enums.BookingStatus.Pending, booking.Status);

        // The booking is the account's own.
        var mine = await (await account.GetAsync("/api/bookings/my")).ReadAsync<List<BookingSummaryDto>>();
        Assert.Contains(mine, b => b.Pnr == booking.Pnr);
    }

    [Fact]
    public async Task ThePriceQuote_IsStillOpen()
    {
        var seed = await _factory.GetSeedAsync();

        var quote = await _factory.CreateClient().PostAsJsonAsync("/api/bookings/quote", BookingEndpointTests.Request(seed, "1B"), Json);

        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
    }
}
