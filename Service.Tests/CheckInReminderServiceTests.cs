using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

/// <summary>The outbound flight departs on 10 Nov 2026 at 09:00 UTC; the reminder goes out in the 24 hours before.</summary>
public class CheckInReminderServiceTests
{
    private readonly BookingTestContext _context = new();

    private CheckInReminderService CreateService(DateTime now, int hours = 24) => new(
        _context.Bookings,
        _context.Notifications,
        new NotificationService(_context.Notifications),
        Options.Create(new BookingLifecycleSettings { CheckInReminderHours = hours }),
        new FixedTimeProvider(now));

    private async Task<BookingDto> BookAndPayAsync(int? userId)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 101)));
        request.Legs[0].CabinClass = CabinClass.Business;
        var booking = await _context.CreateBookingService().CreateBookingAsync(userId, request);
        return await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr));
    }

    [Fact]
    public async Task InsideTheWindow_TheOwnerIsRemindedOnce()
    {
        var booking = await BookAndPayAsync(1);
        var now = TestData.OutboundDeparture.AddHours(-23);

        Assert.Equal(1, (await CreateService(now).QueueDueAsync()).Reminders);
        Assert.Equal(0, (await CreateService(now.AddHours(1)).QueueDueAsync()).Reminders);

        var reminder = Assert.Single(await _context.Notifications.GetByUserIdAsync(1), n => n.Type == NotificationType.CheckInReminder);
        Assert.Equal(booking.Id, reminder.BookingId);
        Assert.Contains(booking.Pnr, reminder.Message);
        Assert.Contains("departs within 24 hours", reminder.Message);
    }

    [Fact]
    public async Task OutsideTheWindow_NobodyIsReminded()
    {
        await BookAndPayAsync(1);

        Assert.Equal(0, (await CreateService(TestData.OutboundDeparture.AddHours(-25)).QueueDueAsync()).Reminders);
        Assert.Equal(0, (await CreateService(TestData.OutboundDeparture.AddMinutes(1)).QueueDueAsync()).Reminders);
        Assert.DoesNotContain(await _context.Notifications.GetByUserIdAsync(1), n => n.Type == NotificationType.CheckInReminder);
    }

    [Fact]
    public async Task GuestBookings_HaveNoNotificationList_SoTheyAreSkipped()
    {
        await BookAndPayAsync(null);

        Assert.Equal(0, (await CreateService(TestData.OutboundDeparture.AddHours(-2)).QueueDueAsync()).Reminders);
    }

    [Fact]
    public async Task TheWindowIsConfigurable()
    {
        await BookAndPayAsync(1);
        var now = TestData.OutboundDeparture.AddHours(-30);

        Assert.Equal(0, (await CreateService(now, hours: 24).QueueDueAsync()).Reminders);
        Assert.Equal(1, (await CreateService(now, hours: 48).QueueDueAsync()).Reminders);
    }

    [Fact]
    public async Task UnpaidBookings_AreNotReminded()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 101)));
        request.Legs[0].CabinClass = CabinClass.Business;
        await _context.CreateBookingService().CreateBookingAsync(1, request);

        Assert.Equal(0, (await CreateService(TestData.OutboundDeparture.AddHours(-2)).QueueDueAsync()).Reminders);
    }
}
