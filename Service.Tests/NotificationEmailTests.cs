using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

/// <summary>Notifications are e-mailed to the address of the account they belong to. No mail server is involved.</summary>
public class NotificationEmailTests
{
    private readonly BookingTestContext _context = new();

    private NotificationEmailService CreateService() => new(
        _context.Notifications,
        _context.Bookings,
        _context.EmailSender,
        Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test/" }),
        _context.Clock,
        new ListLogger<NotificationEmailService>());

    private static User Account(string email, bool verified = true) => new()
    {
        Id = 1,
        Email = email,
        FirstName = "Aysun",
        LastName = "Panahli",
        EmailVerifiedAt = verified ? TestData.Now.AddDays(-3) : null
    };

    private async Task<Notification> NotifyAsync(User user, string title = "Booking Confirmed", string message = "Your booking ABC123 is confirmed.", TimeSpan? age = null)
    {
        var notification = new Notification
        {
            UserId = user.Id,
            User = user,
            Type = NotificationType.BookingConfirmed,
            Title = title,
            Message = message,
            CreatedAt = _context.Clock.GetUtcNow().UtcDateTime - (age ?? TimeSpan.FromMinutes(1))
        };
        await _context.Notifications.AddAsync(notification);
        return notification;
    }

    [Fact]
    public async Task ANotification_IsMailedToTheAccountsOwnAddress_OnceOnly()
    {
        var user = Account("aysun.test@gmail.com");
        var notification = await NotifyAsync(user);

        Assert.Equal(1, await CreateService().SendDueAsync());

        var mail = Assert.Single(_context.EmailSender.Sent);
        Assert.Equal("aysun.test@gmail.com", mail.To);
        Assert.Equal("AEROVA: Booking Confirmed", mail.Subject);
        Assert.Contains("Your booking ABC123 is confirmed.", mail.TextBody);
        Assert.Contains("https://aerova.test/notifications.html", mail.TextBody);
        Assert.Contains("Hello Aysun,", mail.TextBody);
        Assert.NotNull(notification.EmailSentAt);

        // The next run finds nothing left to send.
        Assert.Equal(0, await CreateService().SendDueAsync());
        Assert.Single(_context.EmailSender.Sent);
    }

    [Fact]
    public async Task TheContactAddressOfABooking_NeverReceivesTheMail()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        request.ContactEmail = "someone.else@example.com";
        var booking = await _context.CreateBookingService().CreateBookingAsync(1, request);
        await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr));

        // The two notifications the payment queued belong to account 1, whose address is the one typed at registration.
        var user = Account("registered@gmail.com");
        foreach (var notification in await _context.Notifications.GetAllAsync())
        {
            notification.User = user;
            notification.CreatedAt = _context.Clock.GetUtcNow().UtcDateTime;
        }

        Assert.Equal(2, await CreateService().SendDueAsync());

        Assert.All(_context.EmailSender.Sent, mail => Assert.Equal("registered@gmail.com", mail.To));
        Assert.DoesNotContain(_context.EmailSender.Sent, mail => mail.To.Contains("someone.else"));
        Assert.Contains(_context.EmailSender.Sent, mail => mail.Subject == "AEROVA: Booking Confirmed");
    }

    [Fact]
    public async Task AnAddressThatWasNeverVerified_ReceivesNothing()
    {
        await NotifyAsync(Account("not.mine@gmail.com", verified: false));

        Assert.Equal(0, await CreateService().SendDueAsync());

        Assert.Empty(_context.EmailSender.Sent);
    }

    [Fact]
    public async Task WithoutMailSetup_NothingIsSent_AndNothingIsMarkedSent()
    {
        var notification = await NotifyAsync(Account("aysun.test@gmail.com"));
        _context.EmailSender.IsConfigured = false;

        Assert.Equal(0, await CreateService().SendDueAsync());
        Assert.Null(notification.EmailSentAt);
        Assert.Equal(0, notification.EmailAttempts);

        // Once mail is set up, the same notification goes out.
        _context.EmailSender.IsConfigured = true;
        Assert.Equal(1, await CreateService().SendDueAsync());
    }

    [Fact]
    public async Task OldNews_IsNeverMailed_SoSwitchingMailOnDoesNotFloodAnyone()
    {
        await NotifyAsync(Account("aysun.test@gmail.com"), age: TimeSpan.FromDays(2));

        Assert.Equal(0, await CreateService().SendDueAsync());

        Assert.Empty(_context.EmailSender.Sent);
    }

    [Fact]
    public async Task AFailedDelivery_IsTriedAFewTimes_ThenLeftAlone_WithoutBlockingTheOthers()
    {
        var user = Account("aysun.test@gmail.com");
        var failing = await NotifyAsync(user, "Refund Completed", "Your refund is on its way.");
        _context.EmailSender.Fail = true;

        for (var run = 0; run < 5; run++)
        {
            Assert.Equal(0, await CreateService().SendDueAsync());
        }

        Assert.Equal((3, (DateTime?)null), (failing.EmailAttempts, failing.EmailSentAt));

        // Mail works again: the stuck one stays as it is, a new notification goes out at once.
        _context.EmailSender.Fail = false;
        await NotifyAsync(user, "Booking Cancelled", "Your booking was cancelled.");
        Assert.Equal(1, await CreateService().SendDueAsync());
        Assert.Equal("AEROVA: Booking Cancelled", Assert.Single(_context.EmailSender.Sent).Subject);
        Assert.Null(failing.EmailSentAt);
    }

    [Fact]
    public void TheMailText_IsEncoded_SoNothingInTheMessageCanBecomeMarkup()
    {
        var (subject, text, html) = EmailTemplates.Notification("<b>Aysun</b>", "Hi <script>x</script>", "Seat 1A & 1B <img src=x>", "https://aerova.test/notifications.html");

        Assert.Equal("AEROVA: Hi <script>x</script>", subject);
        Assert.Contains("Seat 1A & 1B <img src=x>", text);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>Aysun</b>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Seat 1A &amp; 1B", html);
    }
}
