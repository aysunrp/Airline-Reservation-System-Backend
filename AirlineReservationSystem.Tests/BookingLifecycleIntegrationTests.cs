using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Repository.Data;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Notification;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Refund processing and trip completion against the real database, run with a clock moved forward.</summary>
public class BookingLifecycleIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BookingLifecycleIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class Clock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public Clock(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private async Task<(HttpClient Client, BookingDto Booking)> BookAndPayAsync(string seat, string lastName)
    {
        var seed = await _factory.GetSeedAsync();
        var client = _factory.CreateClient();
        client.WithToken((await client.RegisterAsync()).AccessToken);
        var booking = await (await client.PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, seat, lastName: lastName), Json)).ReadAsync<BookingDto>();
        var paid = await client.PostAsJsonAsync("/api/payments", new PaymentRequestDto { Pnr = booking.Pnr, Method = PaymentMethod.ApplePay }, Json);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return (client, await paid.ReadAsync<BookingDto>());
    }

    [Fact]
    public async Task CancelledBusinessBooking_IsRefundedInFull()
    {
        // Business ten days out is in the free band of the seeded rules.
        var (client, booking) = await BookAndPayAsync("1A", "Mammadli");
        var cancelled = await (await client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Mammadli" }, Json)).ReadAsync<BookingDto>();
        Assert.Equal(RefundStatus.Pending, cancelled.Cancellation!.RefundStatus);

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var refunds = new RefundProcessingService(services.GetRequiredService<IBookingRepository>(), services.GetRequiredService<INotificationService>(),
                services.GetRequiredService<IOptions<BookingLifecycleSettings>>(), new Clock(DateTime.UtcNow.AddMinutes(31)));
            Assert.True((await refunds.ProcessDueAsync()).Completed >= 1);
        }

        var after = await (await client.GetAsync($"/api/bookings/{booking.Pnr}?lastName=Mammadli")).ReadAsync<BookingDto>();
        Assert.Equal(RefundStatus.Completed, after.Cancellation!.RefundStatus);
        Assert.Equal(PaymentStatus.Refunded, after.PaymentStatus);
        Assert.Contains(await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task FlownTrip_IsCompleted()
    {
        var seed = await _factory.GetSeedAsync();
        var (client, booking) = await BookAndPayAsync("11B", "Huseynli");

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var completion = new TripCompletionService(services.GetRequiredService<IFlightRepository>(), services.GetRequiredService<IBookingRepository>(),
                new Clock(seed.Departure.AddDays(1)));
            var result = await completion.CompleteFinishedAsync();
            Assert.True(result.Bookings >= 1 && result.Flights >= 1);
        }

        Assert.Equal(BookingStatus.Completed, (await (await client.GetAsync("/api/bookings/my")).ReadAsync<List<BookingSummaryDto>>()).Single().Status);
        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(FlightStatus.Completed, (await db.Flights.SingleAsync(f => f.Id == seed.FlightId)).Status);
        Assert.All(await db.Tickets.Where(t => t.BookingPassenger.Booking.Pnr == booking.Pnr).ToListAsync(), t => Assert.Equal(TicketStatus.Used, t.Status));
    }

    [Fact]
    public void Worker_IsRegistered_AndReadsItsSettings()
    {
        Assert.Single(_factory.Services.GetServices<IHostedService>().OfType<BookingLifecycleWorker>());

        // appsettings.json values; the test host turns the worker off so it cannot change test data.
        var settings = _factory.Services.GetRequiredService<IOptions<BookingLifecycleSettings>>().Value;
        Assert.Equal((5, 30, 60, false), (settings.RefundProcessingAfterMinutes, settings.RefundCompletedAfterMinutes, settings.CheckIntervalSeconds, settings.WorkerEnabled));
    }
}
