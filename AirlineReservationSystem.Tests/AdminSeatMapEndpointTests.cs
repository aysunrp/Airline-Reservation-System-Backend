using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.DTOs.Seat;
using Service.Services;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// What the admin seat map is fed with: the persisted status of every seat. A seat is Booked (grey on the map) only once its booking is paid;
/// an unpaid booking only holds it (Reserved), and a cancelled or expired booking gives it back.
/// </summary>
public class AdminSeatMapEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminSeatMapEndpointTests(ApiFactory factory) => _factory = factory;

    private sealed class Clock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public Clock(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private async Task<SeatStatus> StatusOfAsync(HttpClient admin, string seat)
    {
        var seed = await _factory.GetSeedAsync();
        var inventory = await (await admin.GetAsync($"/api/admin/flights/{seed.FlightId}/seats")).ReadAsync<SeatInventoryDto>();
        return inventory.Seats.Single(s => s.Id == seed.SeatIds[seat]).Status;
    }

    private async Task<(HttpClient Client, BookingDto Booking)> BookAsync(string seat, string lastName, bool pay)
    {
        var seed = await _factory.GetSeedAsync();
        var client = _factory.CreateClient();
        client.WithToken((await client.RegisterAsync()).AccessToken);
        var created = await client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, seat, lastName: lastName), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.ReadAsync<BookingDto>();
        if (!pay) return (client, booking);

        var paid = await client.PostAsJsonAsync("/api/payments", new PaymentRequestDto { Pnr = booking.Pnr, Method = PaymentMethod.ApplePay }, Json);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return (client, await paid.ReadAsync<BookingDto>());
    }

    [Fact]
    public async Task AnUnpaidBooking_OnlyHoldsTheSeat_ItBecomesBookedWhenPaid_AndNobodyCanTakeItMeanwhile()
    {
        var admin = await _factory.CreateSuperAdminClientAsync();
        Assert.Equal(SeatStatus.Available, await StatusOfAsync(admin, "11A"));

        var (client, booking) = await BookAsync("11A", "Held", pay: false);
        Assert.Equal(SeatStatus.Held, await StatusOfAsync(admin, "11A")); // reserved, not occupied: not grey

        // The same seat cannot be booked again, nor blocked by an admin, while it is held.
        var seed = await _factory.GetSeedAsync();
        var other = _factory.CreateClient();
        other.WithToken((await other.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A", lastName: "Other"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PatchAsJsonAsync($"/api/admin/flights/{seed.FlightId}/seats/{seed.SeatIds["11A"]}", new SeatBlockRequestDto { Blocked = true }, Json)).StatusCode);

        var paid = await client.PostAsJsonAsync("/api/payments", new PaymentRequestDto { Pnr = booking.Pnr, Method = PaymentMethod.ApplePay }, Json);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal(SeatStatus.Booked, await StatusOfAsync(admin, "11A")); // paid: occupied, grey

        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A", lastName: "Other"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PatchAsJsonAsync($"/api/admin/flights/{seed.FlightId}/seats/{seed.SeatIds["11A"]}", new SeatBlockRequestDto { Blocked = true }, Json)).StatusCode);
    }

    [Fact]
    public async Task ACancelledBooking_GivesItsSeatBack_AndThenOnlyThen()
    {
        var admin = await _factory.CreateSuperAdminClientAsync();
        var (client, booking) = await BookAsync("1A", "Mammadli", pay: true);
        Assert.Equal(SeatStatus.Booked, await StatusOfAsync(admin, "1A"));

        var cancelled = await client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Mammadli" }, Json);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.Equal(SeatStatus.Available, await StatusOfAsync(admin, "1A"));
    }

    [Fact]
    public async Task AnExpiredUnpaidBooking_ReleasesItsSeat_ButAPaidOneKeepsIt()
    {
        var admin = await _factory.CreateSuperAdminClientAsync();
        await BookAsync("11B", "Unpaid", pay: false);
        await BookAsync("11C", "Paid", pay: true);
        Assert.Equal(SeatStatus.Held, await StatusOfAsync(admin, "11B"));
        Assert.Equal(SeatStatus.Booked, await StatusOfAsync(admin, "11C"));

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var seats = new SeatService(services.GetRequiredService<IFlightRepository>(), services.GetRequiredService<IFlightSeatRepository>(), new Clock(DateTime.UtcNow.AddMinutes(60)));
            var expiry = new BookingExpiryService(services.GetRequiredService<IBookingRepository>(), seats, new Clock(DateTime.UtcNow.AddMinutes(60)));
            Assert.True(await expiry.ReleaseExpiredAsync() >= 1);
        }

        Assert.Equal(SeatStatus.Available, await StatusOfAsync(admin, "11B"));
        Assert.Equal(SeatStatus.Booked, await StatusOfAsync(admin, "11C"));
    }
}
