using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A test database where Stripe counts as configured; "Stripe" itself is <see cref="TestStripeGateway"/>.</summary>
public sealed class StripeApiFactory : ApiFactory
{
    public StripeApiFactory()
    {
        StripeConfigured = true;
    }
}

/// <summary>
/// Stripe Checkout over HTTP: who may open the payment page, that the amount is the booking's own, that the page confirms nothing
/// by itself, and who may read a payment's status. No request reaches Stripe and no card is charged.
/// </summary>
public class StripeCheckoutEndpointTests : IClassFixture<StripeApiFactory>, IClassFixture<ApiFactory>
{
    private readonly StripeApiFactory _factory;
    private readonly ApiFactory _withoutStripe;

    public StripeCheckoutEndpointTests(StripeApiFactory factory, ApiFactory withoutStripe)
    {
        _factory = factory;
        _withoutStripe = withoutStripe;
    }

    private static int _nextRow = 30;

    /// <summary>
    /// Every booking needs a seat, and the test aircraft has only five; each booking here gets a fresh Economy seat of its own.
    /// </summary>
    private static async Task<int> NewSeatAsync(ApiFactory factory, SeedData seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flight = await db.Flights.SingleAsync(f => f.Id == seed.FlightId);
        var aircraftSeat = new AircraftSeat { AircraftId = flight.AircraftId, RowNumber = Interlocked.Increment(ref _nextRow), Letter = "A", CabinClass = CabinClass.Economy };
        var flightSeat = new FlightSeat { FlightId = flight.Id, AircraftSeat = aircraftSeat };
        db.FlightSeats.Add(flightSeat);
        await db.SaveChangesAsync();
        return flightSeat.Id;
    }

    private async Task<BookingDto> BookAsync(HttpClient client, ApiFactory? factory = null)
    {
        factory ??= _factory;
        var seed = await factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, null);
        request.Passengers[0].Seats.Add(new PassengerSeatRequestDto { FlightId = seed.FlightId, FlightSeatId = await NewSeatAsync(factory, seed) });
        var response = await client.PostAsJsonAsync("/api/bookings", request, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<BookingDto>();
    }

    private async Task<HttpClient> SignedInClientAsync()
        => _factory.ClientWith((await _factory.CreateClient().RegisterAsync()).AccessToken);

    private static Task<HttpResponseMessage> OpenCheckoutAsync(HttpClient client, string pnr, string? lastName = null)
        => client.PostAsJsonAsync("/api/payments/checkout-session", new { pnr, lastName }, Json);

    [Fact]
    public async Task TheOwner_GetsAStripePage_AndTheBookingStaysUnpaid()
    {
        var client = await SignedInClientAsync();
        var booking = await BookAsync(client);

        var response = await OpenCheckoutAsync(client, booking.Pnr);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.ReadAsync<CheckoutSessionDto>();
        Assert.StartsWith("https://checkout.stripe.test/", session.CheckoutUrl);

        var status = await (await client.GetAsync($"/api/payments/{session.PaymentId}")).ReadAsync<PaymentStatusDto>();
        Assert.Equal((PaymentStatus.Pending, BookingStatus.Pending, booking.Pnr, booking.Price.Total, "USD"),
            (status.Status, status.BookingStatus, status.Pnr, status.Amount, status.Currency));
        Assert.Null(status.PaidAt);

        var after = await (await client.GetAsync($"/api/bookings/my/{booking.Pnr}")).ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Pending, after.Status);
    }

    [Fact]
    public async Task APriceSentByTheBrowser_ChangesNothing()
    {
        var client = await SignedInClientAsync();
        var booking = await BookAsync(client);

        var response = await client.PostAsJsonAsync("/api/payments/checkout-session",
            new { pnr = booking.Pnr, amount = 1, totalPrice = 0.5, discount = 100, currency = "eur" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paymentId = (await response.ReadAsync<CheckoutSessionDto>()).PaymentId;
        var sent = _factory.Stripe.Created.Single(r => r.PaymentId == paymentId);
        Assert.Equal((long)(booking.Price.Total * 100), sent.AmountMinor);
        Assert.Equal("usd", sent.Currency);
    }

    [Fact]
    public async Task SomebodyElse_OrNobody_CannotOpenThePageOfAnAccountsBooking()
    {
        var owner = await SignedInClientAsync();
        var booking = await BookAsync(owner);
        var stranger = await SignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await OpenCheckoutAsync(stranger, booking.Pnr)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await OpenCheckoutAsync(_factory.CreateClient(), booking.Pnr)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await OpenCheckoutAsync(_factory.CreateClient(), booking.Pnr, "Panahli")).StatusCode);
    }

    [Fact]
    public async Task AGuest_OpensThePageWithAPassengersLastName()
    {
        var guest = _factory.CreateClient();
        var booking = await BookAsync(guest);

        Assert.Equal(HttpStatusCode.NotFound, (await OpenCheckoutAsync(guest, booking.Pnr, "Someone")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await OpenCheckoutAsync(guest, booking.Pnr, "panahli")).StatusCode);
    }

    [Fact]
    public async Task TheStatus_IsReadableByTheOwner_AnAdmin_OrWithTheSessionId_Only()
    {
        var owner = await SignedInClientAsync();
        var booking = await BookAsync(owner);
        var session = await (await OpenCheckoutAsync(owner, booking.Pnr)).ReadAsync<CheckoutSessionDto>();
        string sessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            sessionId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.SingleAsync(p => p.Id == session.PaymentId)).StripeCheckoutSessionId!;
        }

        var admin = await _factory.CreateAdminClientAsync();
        var stranger = await SignedInClientAsync();
        var anonymous = _factory.CreateClient();
        var url = $"/api/payments/{session.PaymentId}";

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"{url}?session={sessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{url}?session=cs_test_guess")).StatusCode);
    }

    [Fact]
    public async Task APaidBooking_GetsNoSecondPage()
    {
        var client = await SignedInClientAsync();
        var booking = await BookAsync(client);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Bookings.SingleAsync(b => b.Pnr == booking.Pnr);
            stored.Status = BookingStatus.Confirmed;
            db.Payments.Add(new Payment { BookingId = stored.Id, Amount = stored.TotalAmount, Method = PaymentMethod.StripeCheckout, Status = PaymentStatus.Paid, PaidAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await OpenCheckoutAsync(client, booking.Pnr)).StatusCode);
    }

    [Fact]
    public async Task ThePretendCardPayment_IsRefused_OnceStripeIsConfigured()
    {
        var guest = _factory.CreateClient();
        var booking = await BookAsync(guest);

        var response = await guest.PostAsJsonAsync("/api/payments", new PaymentRequestDto
        {
            Pnr = booking.Pnr, Method = PaymentMethod.Card, CardholderName = "Aysun Panahli", CardNumber = "4111 1111 1111 1111",
            ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 2, Cvv = "123"
        }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task WithoutStripeSecrets_ThePageCannotBeOpened()
    {
        var guest = _withoutStripe.CreateClient();
        var booking = await BookAsync(guest, _withoutStripe);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await OpenCheckoutAsync(guest, booking.Pnr, "Panahli")).StatusCode);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(HttpClient client, string payload, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature ?? StripeTestEvents.Sign(payload, ApiFactory.TestWebhookSecret));
        return await client.SendAsync(request);
    }

    private async Task<Payment> StoredPaymentAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId);
    }

    private async Task<int> NotificationsOfAsync(int bookingId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.CountAsync(n => n.BookingId == bookingId);
    }

    [Fact]
    public async Task StripesSignedEvent_ConfirmsTheBooking_OnceAndForAll()
    {
        var owner = await SignedInClientAsync();
        var booking = await BookAsync(owner);
        var session = await (await OpenCheckoutAsync(owner, booking.Pnr)).ReadAsync<CheckoutSessionDto>();
        var payment = await StoredPaymentAsync(session.PaymentId);
        var cents = (long)(payment.Amount * 100);
        var paid = StripeTestEvents.CheckoutSession($"evt_{Guid.NewGuid():N}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId, cents);

        // Stripe sends no user token: the signature is what counts.
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(anonymous, paid)).StatusCode);

        var status = await (await owner.GetAsync($"/api/payments/{session.PaymentId}")).ReadAsync<PaymentStatusDto>();
        Assert.Equal((PaymentStatus.Paid, BookingStatus.Confirmed), (status.Status, status.BookingStatus));
        Assert.NotNull(status.PaidAt);
        var confirmed = await (await owner.GetAsync($"/api/bookings/my/{booking.Pnr}")).ReadAsync<BookingDto>();
        Assert.Equal(TicketStatus.Issued, confirmed.Passengers.Single().Tickets.Single().Status);
        Assert.Equal(2, await NotificationsOfAsync(payment.BookingId));

        // The same event again, and another event for the same page: nothing is confirmed twice.
        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(anonymous, paid)).StatusCode);
        var async = StripeTestEvents.CheckoutSession($"evt_{Guid.NewGuid():N}", "checkout.session.async_payment_succeeded", payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId, cents);
        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(anonymous, async)).StatusCode);
        Assert.Equal(2, await NotificationsOfAsync(payment.BookingId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(p => p.BookingId == payment.BookingId && p.Status == PaymentStatus.Paid));
        Assert.Equal(2, await db.StripeWebhookEvents.CountAsync(e => e.CheckoutSessionId == payment.StripeCheckoutSessionId));

        // A paid booking opens no new page.
        Assert.Equal(HttpStatusCode.Conflict, (await OpenCheckoutAsync(owner, booking.Pnr)).StatusCode);
    }

    [Fact]
    public async Task AnEventWithAWrongSignature_IsRefused_AndChangesNothing()
    {
        var owner = await SignedInClientAsync();
        var booking = await BookAsync(owner);
        var session = await (await OpenCheckoutAsync(owner, booking.Pnr)).ReadAsync<CheckoutSessionDto>();
        var payment = await StoredPaymentAsync(session.PaymentId);
        var paid = StripeTestEvents.CheckoutSession($"evt_{Guid.NewGuid():N}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId, (long)(payment.Amount * 100));

        var forged = await SendWebhookAsync(_factory.CreateClient(), paid, StripeTestEvents.Sign(paid, "whsec_somebody_else"));
        var unsigned = await SendWebhookAsync(_factory.CreateClient(), paid, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsigned.StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await StoredPaymentAsync(session.PaymentId)).Status);
        var status = await (await owner.GetAsync($"/api/payments/{session.PaymentId}")).ReadAsync<PaymentStatusDto>();
        Assert.Equal(BookingStatus.Pending, status.BookingStatus);
    }

    [Fact]
    public async Task AnEventWithTheWrongAmount_IsAcknowledged_ButConfirmsNothing()
    {
        var owner = await SignedInClientAsync();
        var booking = await BookAsync(owner);
        var session = await (await OpenCheckoutAsync(owner, booking.Pnr)).ReadAsync<CheckoutSessionDto>();
        var payment = await StoredPaymentAsync(session.PaymentId);
        var cheap = StripeTestEvents.CheckoutSession($"evt_{Guid.NewGuid():N}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, payment.BookingId, 100);

        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(_factory.CreateClient(), cheap)).StatusCode);

        var status = await (await owner.GetAsync($"/api/payments/{session.PaymentId}")).ReadAsync<PaymentStatusDto>();
        Assert.Equal((PaymentStatus.Pending, BookingStatus.Pending), (status.Status, status.BookingStatus));
    }

    [Fact]
    public async Task WithoutStripeSecrets_TheWebhookAsksStripeToTryAgain()
    {
        var payload = StripeTestEvents.Other($"evt_{Guid.NewGuid():N}", "customer.created");

        var response = await SendWebhookAsync(_withoutStripe.CreateClient(), payload);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
