using Domain.Entities;
using Domain.Enums;
using Service.Helpers.Mappings;

namespace Service.Tests;

/// <summary>A support notification tells the page which ticket it is about, so the page can open that conversation directly.</summary>
public class NotificationTicketLinkTests
{
    private static Notification Make(NotificationType type, string? key)
        => new() { Id = 1, UserId = 1, Type = type, Title = "t", Message = "m", EmailKey = key };

    [Theory]
    [InlineData("support:42:created", 42)]
    [InlineData("support:42:reply:7", 42)]
    [InlineData("support:42:status:638000000000000000", 42)]
    public void ASupportNotification_CarriesItsTicketId(string key, int ticketId)
        => Assert.Equal(ticketId, Make(NotificationType.SupportUpdate, key).ToDto().SupportTicketId);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("support:abc:reply:1")]
    [InlineData("support:0:created")]
    [InlineData("support:5")]
    [InlineData("refund:12:1:completed")]
    public void AMalformedOrForeignKey_GivesNoTicketId(string? key)
        => Assert.Null(Make(NotificationType.SupportUpdate, key).ToDto().SupportTicketId);

    [Fact]
    public void OtherNotificationTypes_NeverGetATicketId()
        => Assert.Null(Make(NotificationType.BookingConfirmed, "support:42:created").ToDto().SupportTicketId);
}
