using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Two customers send a booking for the same seat at the same moment.</summary>
public class ConcurrentBookingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConcurrentBookingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string Pnr)> BookAsync(SeedData seed, string seat, string lastName, string document)
    {
        var client = _factory.CreateClient();
        client.WithToken((await client.RegisterAsync()).AccessToken);
        var request = BookingEndpointTests.Request(seed, seat, lastName: lastName);
        request.Passengers[0].DocumentNumber = document;
        var created = await client.PostAsJsonAsync("/api/bookings", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (client, (await created.ReadAsync<Service.Helpers.DTOs.Reservation.BookingDto>()).Pnr);
    }

    [Fact]
    public async Task SameSeatChosenByTwo_BothMayChooseIt_ButOnlyOnePaymentGetsIt()
    {
        var seed = await _factory.GetSeedAsync();

        // Repeated a few times so both orders of the race are likely to happen at least once.
        foreach (var (seat, round) in new[] { ("11A", 1), ("11B", 2), ("11C", 3) })
        {
            var first = await BookAsync(seed, seat, $"First{round}", "C01234567");
            var second = await BookAsync(seed, seat, $"Second{round}", "C07654321");

            // Choosing reserves nothing: both bookings exist and the seat is still free.
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(SeatStatus.Available, (await db.FlightSeats.SingleAsync(s => s.Id == seed.SeatIds[seat])).Status);
            }

            var responses = await Task.WhenAll(
                first.Client.PostAsJsonAsync("/api/payments", new Service.Helpers.DTOs.Payment.PaymentRequestDto { Pnr = first.Pnr, Method = PaymentMethod.ApplePay }, Json),
                second.Client.PostAsJsonAsync("/api/payments", new Service.Helpers.DTOs.Payment.PaymentRequestDto { Pnr = second.Pnr, Method = PaymentMethod.ApplePay }, Json));

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).Order());

            using var check = _factory.Services.CreateScope();
            var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatId = seed.SeatIds[seat];
            Assert.Equal(1, await context.Tickets.CountAsync(t => t.FlightSeatId == seatId && t.Status == TicketStatus.Issued));
            Assert.Equal(SeatStatus.Booked, (await context.FlightSeats.SingleAsync(s => s.Id == seatId)).Status);
        }
    }
}
