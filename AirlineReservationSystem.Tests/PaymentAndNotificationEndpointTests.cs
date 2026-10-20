using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Notification;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class PaymentAndNotificationEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PaymentAndNotificationEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static PaymentRequestDto Card(string pnr) => new()
    {
        Pnr = pnr,
        Method = PaymentMethod.Card,
        CardholderName = "Aysun Panahli",
        CardNumber = "4111 1111 1111 1111",
        ExpiryMonth = 12,
        ExpiryYear = DateTime.UtcNow.Year + 2,
        Cvv = "123"
    };

    private async Task<BookingDto> BookAsync(HttpClient client, string seat)
    {
        var seed = await _factory.GetSeedAsync();
        var response = await client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, seat), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<BookingDto>();
    }

    [Fact]
    public async Task Payment_ConfirmsTheBooking_AndCannotBeRepeated()
    {
        var client = _factory.CreateClient();
        var booking = await BookAsync(client, "11A");

        var paid = await client.PostAsJsonAsync("/api/payments", Card(booking.Pnr), Json);

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var confirmed = await paid.ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Confirmed, confirmed.Status);
        Assert.Equal(PaymentStatus.Paid, confirmed.PaymentStatus);
        Assert.Equal(TicketStatus.Issued, confirmed.Passengers.Single().Tickets.Single().Status);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Card(booking.Pnr), Json)).StatusCode);
    }

    [Fact]
    public async Task Payment_InvalidCardOrUnknownBooking()
    {
        var client = _factory.CreateClient();
        var booking = await BookAsync(client, "11B");

        var badCard = Card(booking.Pnr);
        badCard.CardNumber = "4111 1111 1111 1112";
        var invalid = await client.PostAsJsonAsync("/api/payments", badCard, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await invalid.ReadJsonAsync()).GetProperty("errors").TryGetProperty("CardNumber", out _));

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/payments", Card("ZZZZZZ"), Json)).StatusCode);
    }

    [Fact]
    public async Task Notifications_FollowTheBookingAndCanBeMarkedRead()
    {
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);
        var booking = await BookAsync(client, "1A");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments", Card(booking.Pnr), Json)).StatusCode);

        var notifications = await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.Equal(
            new[] { NotificationType.BookingConfirmed, NotificationType.PaymentSuccessful },
            notifications.Select(n => n.Type).Order());
        Assert.Equal(2, await (await client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync($"/api/notifications/{notifications[0].Id}/read", null)).StatusCode);
        Assert.Equal(1, await (await client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, await (await client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>());

        // Someone else's notification is reported as missing; without a token the endpoints are closed.
        var stranger = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsync($"/api/notifications/{notifications[0].Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/notifications")).StatusCode);
    }

    [Fact]
    public async Task Notifications_CanBeDeletedOneByOne_AndCleared()
    {
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);
        var booking = await BookAsync(client, "1B");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments", Card(booking.Pnr), Json)).StatusCode);
        var notifications = await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.Equal(2, notifications.Count);

        // Someone else cannot delete it; the owner can, and a second delete finds nothing.
        var stranger = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/notifications/{notifications[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/notifications/{notifications[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/notifications/{notifications[0].Id}")).StatusCode);
        Assert.Single(await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>());

        // Clear removes the rest, and clearing an empty list is fine.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/notifications")).StatusCode);
        Assert.Empty(await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().DeleteAsync("/api/notifications")).StatusCode);
    }
}
