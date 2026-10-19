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

    [Fact]
    public async Task SameSeatAtTheSameTime_OnlyOneBookingGetsIt()
    {
        var seed = await _factory.GetSeedAsync();

        // Repeated a few times so both orders of the race are likely to happen at least once.
        foreach (var (seat, round) in new[] { ("11A", 1), ("11B", 2), ("11C", 3) })
        {
            var first = BookingEndpointTests.Request(seed, seat, lastName: $"First{round}");
            var second = BookingEndpointTests.Request(seed, seat, lastName: $"Second{round}");
            second.Passengers[0].DocumentNumber = "C07654321";

            var responses = await Task.WhenAll(
                _factory.CreateClient().PostAsJsonAsync("/api/bookings", first, Json),
                _factory.CreateClient().PostAsJsonAsync("/api/bookings", second, Json));

            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).Order());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatId = seed.SeatIds[seat];
            Assert.Equal(1, await db.Tickets.CountAsync(t => t.FlightSeatId == seatId && t.Status != TicketStatus.Cancelled));
            Assert.Equal(SeatStatus.Held, (await db.FlightSeats.SingleAsync(s => s.Id == seatId)).Status);
        }
    }
}
