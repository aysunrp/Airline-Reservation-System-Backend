using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Support;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

internal sealed class RecordingPublisher : ISupportRealtimePublisher
{
    public List<SupportRealtimeEvent> Staff { get; } = new();
    public List<(int UserId, SupportRealtimeEvent Event)> Customers { get; } = new();
    public Action<SupportRealtimeEvent>? OnPublish { get; set; }
    public bool Throw { get; set; }

    public Task ToStaffAsync(SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new InvalidOperationException("The real-time channel is down.");
        OnPublish?.Invoke(supportEvent);
        Staff.Add(supportEvent);
        return Task.CompletedTask;
    }

    public Task ToCustomerAsync(int userId, SupportRealtimeEvent supportEvent, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new InvalidOperationException("The real-time channel is down.");
        OnPublish?.Invoke(supportEvent);
        Customers.Add((userId, supportEvent));
        return Task.CompletedTask;
    }
}

/// <summary>Real-time support events: who gets which event, that they are published only after the data is saved, and that a down channel loses nothing.</summary>
public class SupportRealtimeTests
{
    private readonly MovableClock _clock = new();
    private readonly FakeUserRepository _users = new(
        new User { Id = 1, FirstName = "Ayla", LastName = "Mammadova", Email = "ayla@example.com", EmailVerifiedAt = new DateTime(2026, 1, 1) },
        new User { Id = 2, FirstName = "Orkhan", LastName = "Aliyev", Email = "orkhan@example.com", EmailVerifiedAt = new DateTime(2026, 1, 1) },
        new User { Id = 9, FirstName = "Staff", LastName = "One", Email = "staff1@aerova.test", Role = UserRole.Admin },
        new User { Id = 10, FirstName = "Staff", LastName = "Two", Email = "staff2@aerova.test", Role = UserRole.SuperAdmin },
        new User { Id = 11, FirstName = "Former", LastName = "Staff", Email = "former@aerova.test", Role = UserRole.Admin, IsActive = false });
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAdminAlertRepository _alerts = new();
    private readonly FakeEmailSender _sender = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly FakeSupportTicketRepository _tickets;

    public SupportRealtimeTests() => _tickets = new FakeSupportTicketRepository(_users);

    private SupportService Create()
    {
        var alerts = new AdminAlertService(_alerts, _sender, Options.Create(new AdminNotificationSettings { RecipientEmail = "admin@aerova.test" }),
            Options.Create(new SmtpSettings()), Options.Create(new AuthSettings()), _clock, NullLogger<AdminAlertService>.Instance);
        return new SupportService(_tickets, _users, new NotificationService(_notifications), alerts, new CreateSupportTicketRequestValidator(),
            new SupportReplyRequestValidator(), new AdminSupportReplyRequestValidator(), new SupportStatusRequestValidator(), Options.Create(new SupportSettings()), _clock,
            _publisher, NullLogger<SupportService>.Instance);
    }

    private static CreateSupportTicketRequestDto Request(string subject = "Seat change")
        => new() { Category = SupportCategory.Booking, Subject = subject, Message = "I need help with my seat selection, please.", BookingPnr = "AB12CD" };

    private IReadOnlyList<Notification> Notifications => _notifications.GetAllAsync().Result;

    [Fact]
    public async Task ANewRequest_GoesToTheStaffGroupOnly_WithTheTicketNumberSubjectAndTopic_AndNothingPrivate()
    {
        var detail = await Create().CreateAsync(1, Request());

        var sent = Assert.Single(_publisher.Staff);
        Assert.Empty(_publisher.Customers);
        Assert.Equal("ticketCreated", sent.Type);
        Assert.Equal(detail.TicketNumber, sent.TicketNumber);
        Assert.Equal(detail.Id, sent.TicketId);
        Assert.Equal("Seat change", sent.Subject);
        Assert.Equal("Booking", sent.Category);
        Assert.Equal("Open", sent.Status);
        // The event type has no field for a message text or an e-mail address.
        Assert.DoesNotContain(typeof(SupportRealtimeEvent).GetProperties(), p => p.Name.Contains("Email") || p.Name.Contains("Message") || p.Name.Contains("Body"));
    }

    [Fact]
    public async Task EveryActiveStaffAccount_GetsABellNotification_ButNoCustomerAndNoFormerStaff()
    {
        var detail = await Create().CreateAsync(1, Request());

        var staff = Notifications.Where(n => n.Type == NotificationType.SupportStaff).ToList();
        Assert.Equal(new[] { 9, 10 }, staff.Select(n => n.UserId).OrderBy(i => i));
        Assert.All(staff, n =>
        {
            Assert.Contains(detail.TicketNumber, n.Title);
            Assert.Contains("Seat change", n.Message);
            Assert.DoesNotContain("ayla@example.com", n.Message);
        });
        Assert.Equal(staff.Count, staff.Select(n => n.EmailKey).Distinct().Count());
        // The customer's own notification is the one that is e-mailed to them.
        Assert.Single(Notifications, n => n.Type == NotificationType.SupportUpdate && n.UserId == 1);
        Assert.DoesNotContain(Notifications, n => n.UserId is 2 or 11);
    }

    [Fact]
    public async Task ARepeatedRequest_PublishesNothingNew_AndAddsNoNotification()
    {
        var service = Create();
        await service.CreateAsync(1, Request());
        var notifications = Notifications.Count;

        await service.CreateAsync(1, Request());

        Assert.Single(_publisher.Staff);
        Assert.Equal(notifications, Notifications.Count);
    }

    [Fact]
    public async Task ACustomerReply_GoesToStaff_AndGivesEachStaffAccountOneMoreNotification()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        _publisher.Staff.Clear();

        await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Here are more details about the problem." });

        var sent = Assert.Single(_publisher.Staff);
        Assert.Equal("customerReplied", sent.Type);
        Assert.Empty(_publisher.Customers);
        Assert.Equal(2, Notifications.Count(n => n.Type == NotificationType.SupportStaff && n.Title.StartsWith("Customer reply")));
        // The same reply sent twice changes nothing.
        await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Here are more details about the problem." });
        Assert.Single(_publisher.Staff);
    }

    [Fact]
    public async Task AStaffReply_GoesToTheOwnerOnly_AndTheOtherStaffSeeThatTheTicketChanged()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        await service.CreateAsync(2, Request("Another customer"));
        _publisher.Staff.Clear();

        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "Which seat would you like?" });

        var toCustomer = Assert.Single(_publisher.Customers);
        Assert.Equal(1, toCustomer.UserId);
        Assert.Equal("staffReplied", toCustomer.Event.Type);
        Assert.Equal("WaitingForCustomer", toCustomer.Event.Status);
        Assert.Null(toCustomer.Event.Subject);
        var toStaff = Assert.Single(_publisher.Staff);
        Assert.Equal("ticketUpdated", toStaff.Type);
        Assert.DoesNotContain(_publisher.Customers, c => c.UserId == 2);
    }

    [Fact]
    public async Task AStatusChange_GoesToTheOwner_ButTheSameStatusAgainGoesToNobody()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        _publisher.Staff.Clear();

        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });
        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });

        var toCustomer = Assert.Single(_publisher.Customers);
        Assert.Equal((1, "statusChanged", "InProgress"), (toCustomer.UserId, toCustomer.Event.Type, toCustomer.Event.Status));
        Assert.Single(_publisher.Staff);
    }

    [Fact]
    public async Task AnEvent_IsPublishedOnlyAfterTheDataIsSaved_AndEveryEventHasItsOwnId()
    {
        var service = Create();
        var messagesWhenPublished = new List<int>();
        _publisher.OnPublish = e => messagesWhenPublished.Add(_tickets.All.Single(t => t.Id == e.TicketId).Messages.Count(m => m.Id > 0));

        var ticket = await service.CreateAsync(1, Request());
        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "We are looking into it now." });

        // The message of every event was already saved (it has an id) when the event went out.
        Assert.Equal(new[] { 1, 2, 2 }, messagesWhenPublished);
        var ids = _publisher.Staff.Select(e => e.EventId).Concat(_publisher.Customers.Select(c => c.Event.EventId)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Fact]
    public async Task WhenTheRealTimeChannelIsDown_TheTicketTheReplyAndTheNotificationsAreStillSaved()
    {
        _publisher.Throw = true;
        var service = Create();

        var ticket = await service.CreateAsync(1, Request());
        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "We are looking into it now." });
        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.Resolved });

        Assert.Single(_tickets.All);
        Assert.Equal(2, _tickets.All[0].Messages.Count);
        Assert.Equal(SupportStatus.Resolved, _tickets.All[0].Status);
        Assert.Contains(Notifications, n => n.Type == NotificationType.SupportUpdate && n.UserId == 1 && n.EmailKey!.Contains(":reply:"));
        Assert.Contains(Notifications, n => n.Type == NotificationType.SupportStaff);
        Assert.NotEmpty(_alerts.All);
    }
}
