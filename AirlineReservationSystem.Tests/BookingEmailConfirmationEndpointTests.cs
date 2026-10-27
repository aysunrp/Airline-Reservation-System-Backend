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

/// <summary>The rules of the real API: sign-in to book, Stripe configured, and a booking confirmed by its own e-mailed link before it is paid.</summary>
public sealed class BookingLinkApiFactory : ApiFactory
{
    public BookingLinkApiFactory()
    {
        StripeConfigured = true;
        RequireSignInToBook = true;
        RequireBookingEmailConfirmation = true;
    }
}

/// <summary>
/// Over HTTP: a booking asks for its own link, payment is refused until the link was opened (by anybody holding it, no sign-in), then
/// payment opens once; each further booking of the same account needs another link. No mail leaves the machine and no card is charged.
/// </summary>
public class BookingEmailConfirmationEndpointTests : IClassFixture<BookingLinkApiFactory>
{
    private const string ContactEmail = "aysun@example.com";

    private static int _nextRow = 60;

    private readonly BookingLinkApiFactory _factory;

    public BookingEmailConfirmationEndpointTests(BookingLinkApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = true;
        _factory.Emails.Fail = false;
    }

    private async Task<int> NewSeatAsync(SeedData seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flight = await db.Flights.SingleAsync(f => f.Id == seed.FlightId);
        var aircraftSeat = new AircraftSeat { AircraftId = flight.AircraftId, RowNumber = Interlocked.Increment(ref _nextRow), Letter = "A", CabinClass = CabinClass.Economy };
        var flightSeat = new FlightSeat { FlightId = flight.Id, AircraftSeat = aircraftSeat };
        db.FlightSeats.Add(flightSeat);
        await db.SaveChangesAsync();
        return flightSeat.Id;
    }

    private async Task<(HttpClient Customer, string Email)> SignedInAsync()
    {
        var email = UniqueEmail("booklink");
        var account = await _factory.CreateClient().RegisterAsync(email);
        return (_factory.ClientWith(account.AccessToken), email);
    }

    private async Task<BookingDto> BookAsync(HttpClient customer)
    {
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, null);
        request.Passengers[0].Seats.Add(new PassengerSeatRequestDto { FlightId = seed.FlightId, FlightSeatId = await NewSeatAsync(seed) });
        var response = await customer.PostAsJsonAsync("/api/bookings", request, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<BookingDto>();
    }

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, string pnr)
        => client.PostAsJsonAsync("/api/payments/checkout-session", new { pnr, lastName = (string?)null }, Json);

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string token)
        => client.PostAsJsonAsync("/api/bookings/confirm-email", new VerifyEmailRequestDto { Token = token }, Json);

    [Fact]
    public async Task ABooking_WaitsForItsLink_PaymentIsRefused_AndOpensOnceTheLinkWasOpened()
    {
        var (customer, _) = await SignedInAsync();
        var booking = await BookAsync(customer);

        Assert.False(booking.ContactConfirmed);
        var token = _factory.Emails.TokenFor(ContactEmail);
        Assert.Contains("confirm-booking.html", _factory.Emails.Sent.Last(m => m.To == ContactEmail).TextBody);

        var refused = await PayAsync(customer, booking.Pnr);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("booking_unconfirmed", (await refused.ReadJsonAsync()).GetProperty("code").GetString());

        // The link needs no sign-in: it is opened in whatever browser the mail program uses.
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(_factory.CreateClient(), token)).StatusCode);

        var mine = await (await customer.GetAsync($"/api/bookings/my/{booking.Pnr}")).ReadAsync<BookingDto>();
        Assert.True(mine.ContactConfirmed);
        Assert.Equal(HttpStatusCode.OK, (await PayAsync(customer, booking.Pnr)).StatusCode);

        // Opening it again changes nothing and says the booking is already confirmed.
        var again = await ConfirmAsync(_factory.CreateClient(), token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal("link_confirmed", (await again.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ASecondBookingOfTheSameAccount_NeedsItsOwnLink_TheFirstOnesDoesNotCountForIt()
    {
        var (customer, _) = await SignedInAsync();
        var first = await BookAsync(customer);
        var firstToken = _factory.Emails.TokenFor(ContactEmail);
        await ConfirmAsync(_factory.CreateClient(), firstToken);

        var second = await BookAsync(customer);

        Assert.False(second.ContactConfirmed);
        Assert.Equal(HttpStatusCode.OK, (await PayAsync(customer, first.Pnr)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PayAsync(customer, second.Pnr)).StatusCode);

        var secondToken = _factory.Emails.TokenFor(ContactEmail);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(_factory.CreateClient(), secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PayAsync(customer, second.Pnr)).StatusCode);
    }

    [Fact]
    public async Task AWrongLink_IsRefused_AndSaysNothingAboutBookings()
    {
        var response = await ConfirmAsync(_factory.CreateClient(), "not-a-real-token-0123456789abcdef");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("link_invalid", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Resend_NeedsSignIn_IsForTheOwnBookingOnly_AndOneLinkPerMinute()
    {
        var (customer, _) = await SignedInAsync();
        var booking = await BookAsync(customer);
        var body = new ResendVerificationRequestDto();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync($"/api/bookings/{booking.Pnr}/resend-confirmation", body, Json)).StatusCode);

        var stranger = (await SignedInAsync()).Customer;
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/resend-confirmation", body, Json)).StatusCode);

        var tooSoon = await customer.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/resend-confirmation", body, Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
        Assert.True(int.Parse(tooSoon.Headers.GetValues("Retry-After").Single()) > 0);
    }
}
