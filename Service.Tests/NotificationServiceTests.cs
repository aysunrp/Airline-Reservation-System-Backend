using Domain.Entities;
using Domain.Enums;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

public class NotificationServiceTests
{
    private readonly FakeNotificationRepository _repository = new(
        new Notification { Id = 1, UserId = 1, Type = NotificationType.BookingConfirmed, Title = "Booking Confirmed", Message = "m", CreatedAt = new DateTime(2026, 10, 1) },
        new Notification { Id = 2, UserId = 1, Type = NotificationType.PaymentSuccessful, Title = "Payment Successful", Message = "m", CreatedAt = new DateTime(2026, 10, 2) },
        new Notification { Id = 3, UserId = 1, Type = NotificationType.FlightUpdate, Title = "Flight Update", Message = "m", IsRead = true, CreatedAt = new DateTime(2026, 10, 3) },
        new Notification { Id = 4, UserId = 2, Type = NotificationType.BookingCancelled, Title = "Booking Cancelled", Message = "m", CreatedAt = new DateTime(2026, 10, 4) });

    private NotificationService CreateService() => new(_repository);

    [Fact]
    public async Task Get_ReturnsOnlyOwnNotificationsNewestFirst()
    {
        var list = await CreateService().GetAsync(1);

        Assert.Equal(new[] { 3, 2, 1 }, list.Select(n => n.Id));
        Assert.Equal(2, await CreateService().GetUnreadCountAsync(1));
    }

    [Fact]
    public async Task MarkAsRead_MarksSingleNotification()
    {
        var service = CreateService();
        await service.MarkAsReadAsync(1, 2);

        Assert.True((await _repository.GetByIdAsync(2))!.IsRead);
        Assert.Equal(1, await service.GetUnreadCountAsync(1));
    }

    [Fact]
    public async Task MarkAsRead_AnotherUsersNotification_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().MarkAsReadAsync(1, 4));
        Assert.False((await _repository.GetByIdAsync(4))!.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_OnlyAffectsTheUser()
    {
        var service = CreateService();
        await service.MarkAllAsReadAsync(1);

        Assert.Equal(0, await service.GetUnreadCountAsync(1));
        Assert.Equal(1, await service.GetUnreadCountAsync(2));
    }

    [Fact]
    public async Task Delete_RemovesOnlyThatNotification()
    {
        var service = CreateService();

        await service.DeleteAsync(1, 2);

        Assert.Equal(new[] { 3, 1 }, (await service.GetAsync(1)).Select(n => n.Id));
        Assert.Equal(1, await service.GetUnreadCountAsync(1));
    }

    [Fact]
    public async Task Delete_AnotherUsersNotification_IsNotFound_AndKeepsIt()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteAsync(1, 4));

        Assert.NotNull(await _repository.GetByIdAsync(4));
    }

    [Fact]
    public async Task Clear_RemovesEveryNotificationOfTheUser_AndNoOneElses()
    {
        var service = CreateService();

        await service.ClearAsync(1);

        Assert.Empty(await service.GetAsync(1));
        Assert.Single(await service.GetAsync(2));
        await service.ClearAsync(1);
    }
}
