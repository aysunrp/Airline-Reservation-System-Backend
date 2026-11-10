using System.Text.RegularExpressions;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Support;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

internal sealed class FakeSupportTicketRepository : FakeRepository<SupportTicket>, ISupportTicketRepository
{
    private readonly FakeUserRepository _users;
    private int _nextId = 1;
    private int _nextMessageId = 1;

    public FakeSupportTicketRepository(FakeUserRepository users) => _users = users;

    public IReadOnlyList<SupportTicket> All => Items;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var ticket in Items)
        {
            if (ticket.Id == 0)
            {
                ticket.Id = _nextId++;
            }

            ticket.User ??= _users.Find(ticket.UserId)!;
            foreach (var message in ticket.Messages.Where(m => m.Id == 0))
            {
                message.Id = _nextMessageId++;
                message.TicketId = ticket.Id;
            }
        }

        return Task.FromResult(1);
    }

    public Task<SupportTicket?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<bool> NumberExistsAsync(string ticketNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(t => t.TicketNumber == ticketNumber));

    public Task<IReadOnlyList<(SupportTicket Ticket, int MessageCount)>> GetForUserAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<(SupportTicket, int)>>(Items.Where(t => t.UserId == userId).OrderByDescending(t => t.LastMessageAt).Select(t => (t, t.Messages.Count)).ToList());

    public Task<int> CountOpenForUserAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(t => t.UserId == userId && t.Status != SupportStatus.Resolved));

    public Task<SupportTicket?> FindRecentDuplicateAsync(int userId, string subject, DateTime since, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(t => t.UserId == userId && t.Subject == subject && t.CreatedAt >= since).OrderByDescending(t => t.Id).FirstOrDefault());

    public Task<(IReadOnlyList<(SupportTicket Ticket, int MessageCount)> Items, int Total)> SearchAsync(
        string? search, SupportStatus? status, SupportCategory? category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Items.AsEnumerable();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (category is not null) query = query.Where(t => t.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var ci = StringComparison.OrdinalIgnoreCase; // SQL Server compares text without regard to case
            query = query.Where(t => t.TicketNumber.Contains(term, ci) || t.Subject.Contains(term, ci) || (t.BookingPnr?.Contains(term, ci) ?? false) || t.User.Email.Contains(term, ci));
        }

        var list = query.OrderByDescending(t => t.LastMessageAt).ToList();
        IReadOnlyList<(SupportTicket, int)> items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(t => (t, t.Messages.Count)).ToList();
        return Task.FromResult((items, list.Count));
    }
}

/// <summary>The support system: tickets, ownership, replies, statuses, and the notifications that become e-mails. The mail server is never involved here.</summary>
public class SupportTests
{
    private readonly MovableClock _clock = new();
    private readonly FakeUserRepository _users = new(
        new User { Id = 1, FirstName = "Ayla", LastName = "Mammadova", Email = "ayla@example.com", EmailVerifiedAt = new DateTime(2026, 1, 1) },
        new User { Id = 2, FirstName = "Orkhan", LastName = "Aliyev", Email = "orkhan@example.com", EmailVerifiedAt = new DateTime(2026, 1, 1) },
        new User { Id = 9, FirstName = "Staff", LastName = "Member", Email = "staff@aerova.test" });
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAdminAlertRepository _alerts = new();
    private readonly FakeEmailSender _sender = new();
    private readonly FakeSupportTicketRepository _tickets;

    public SupportTests() => _tickets = new FakeSupportTicketRepository(_users);

    private SupportService Create(SupportSettings? settings = null)
    {
        var alerts = new AdminAlertService(_alerts, _sender, Options.Create(new AdminNotificationSettings { RecipientEmail = "admin@aerova.test" }),
            Options.Create(new SmtpSettings()), Options.Create(new AuthSettings()), _clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminAlertService>.Instance);
        return new SupportService(_tickets, _users, new NotificationService(_notifications), alerts, new CreateSupportTicketRequestValidator(),
            new SupportReplyRequestValidator(), new AdminSupportReplyRequestValidator(), new SupportStatusRequestValidator(), Options.Create(settings ?? new SupportSettings()), _clock);
    }

    private static CreateSupportTicketRequestDto Request(string subject = "Seat change", string message = "I need help with my seat selection, please.", string? pnr = "ab12cd", SupportCategory category = SupportCategory.Booking)
        => new() { Category = category, Subject = subject, Message = message, BookingPnr = pnr };

    private IReadOnlyList<Notification> Notifications => _notifications.GetAllAsync().Result;

    // ---------------- creating ----------------

    [Fact]
    public async Task ACustomer_CanOpenARequest_WithAUniqueNumber_AndItsFirstMessage()
    {
        var detail = await Create().CreateAsync(1, Request());

        Assert.Matches(new Regex("^SUP-[A-HJ-NP-Z2-9]{6}$"), detail.TicketNumber);
        Assert.Equal(SupportStatus.Open, detail.Status);
        Assert.Equal("AB12CD", detail.BookingPnr);
        Assert.True(detail.CanReply);
        var message = Assert.Single(detail.Messages);
        Assert.Equal(SupportAuthorType.Customer, message.AuthorType);
        Assert.Equal("Ayla Mammadova", message.AuthorName);
        Assert.Equal("I need help with my seat selection, please.", message.Body);

        var second = await Create().CreateAsync(1, Request(subject: "Another problem"));
        Assert.NotEqual(detail.TicketNumber, second.TicketNumber);
    }

    [Theory]
    [InlineData("", "I need help with my seat selection, please.", "AB12CD", SupportCategory.Booking)]
    [InlineData("   ", "I need help with my seat selection, please.", "AB12CD", SupportCategory.Booking)]
    [InlineData("Seat", "short", "AB12CD", SupportCategory.Booking)]
    [InlineData("Seat", "I need help with my seat selection, please.", "no way!", SupportCategory.Booking)]
    [InlineData("Seat", "I need help with my seat selection, please.", "AB1", SupportCategory.Booking)]
    [InlineData("Seat", "I need help with my seat selection, please.", "AB12CD", (SupportCategory)99)]
    public async Task InvalidInput_IsRefused_AndNothingIsStored(string subject, string message, string? pnr, SupportCategory category)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Create().CreateAsync(1, Request(subject, message, pnr, category)));

        Assert.Empty(_tickets.All);
        Assert.Empty(Notifications);
        Assert.Empty(_alerts.All);
    }

    [Fact]
    public async Task TooLongTexts_AreRefused()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Create().CreateAsync(1, Request(subject: new string('s', 121))));
        await Assert.ThrowsAsync<ValidationException>(() => Create().CreateAsync(1, Request(message: new string('m', 4001))));
    }

    [Fact]
    public async Task ABookingReference_IsOptional()
    {
        var detail = await Create().CreateAsync(1, Request(pnr: null, category: SupportCategory.WebsiteProblem));

        Assert.Null(detail.BookingPnr);
    }

    [Fact]
    public async Task CreatingARequest_QueuesOneNotificationForTheCustomer_AndOneAlertForTheAdmin()
    {
        var detail = await Create().CreateAsync(1, Request());

        var notification = Assert.Single(Notifications);
        Assert.Equal(1, notification.UserId);
        Assert.Equal(NotificationType.SupportUpdate, notification.Type);
        Assert.Contains(detail.TicketNumber, notification.Title);
        Assert.Contains("Seat change", notification.Message);
        Assert.Equal($"support:{detail.Id}:created", notification.EmailKey);

        var alert = Assert.Single(_alerts.All);
        Assert.Contains(detail.TicketNumber, alert.Subject);
        Assert.Contains("ayla@example.com", alert.Body);
        Assert.Contains("AB12CD", alert.Body);
    }

    [Fact]
    public async Task TheSameRequestSentAgain_IsTheSameTicket_WithNoSecondEmail()
    {
        var service = Create();
        var first = await service.CreateAsync(1, Request());
        var again = await service.CreateAsync(1, Request());

        Assert.Equal(first.Id, again.Id);
        Assert.Single(_tickets.All);
        Assert.Single(Notifications);
        Assert.Single(_alerts.All);

        // A different message with the same subject, or the same request long after, is a new request.
        await service.CreateAsync(1, Request(message: "A completely different problem with the same subject."));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await service.CreateAsync(1, Request());
        Assert.Equal(3, _tickets.All.Count);
    }

    [Fact]
    public async Task ACustomerWithTooManyOpenRequests_MustWait()
    {
        var service = Create(new SupportSettings { MaxOpenTicketsPerUser = 2 });
        await service.CreateAsync(1, Request(subject: "One"));
        await service.CreateAsync(1, Request(subject: "Two"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(1, Request(subject: "Three")));
        Assert.Equal("too_many_open_tickets", ex.Code);
        // Somebody else is not affected.
        await service.CreateAsync(2, Request(subject: "Three"));
    }

    [Fact]
    public async Task ASignedOutOrUnknownAccount_CannotOpenARequest()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Create().CreateAsync(404, Request()));
        Assert.Empty(_tickets.All);
    }

    // ---------------- ownership ----------------

    [Fact]
    public async Task ACustomer_SeesOnlyTheirOwnRequests()
    {
        var service = Create();
        var mine = await service.CreateAsync(1, Request(subject: "Mine"));
        var theirs = await service.CreateAsync(2, Request(subject: "Theirs"));

        var list = await service.GetMineAsync(1);

        Assert.Equal(new[] { mine.Id }, list.Select(t => t.Id));
        Assert.DoesNotContain(list, t => t.Id == theirs.Id);
        Assert.Equal(1, list[0].MessageCount);
    }

    [Fact]
    public async Task AnotherCustomersRequest_LooksLikeOneThatDoesNotExist_ToRead_ToReply_AndToChange()
    {
        var service = Create();
        var theirs = await service.CreateAsync(2, Request(subject: "Theirs"));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMineAsync(1, theirs.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReplyAsync(1, theirs.Id, new SupportReplyRequestDto { Message = "I am not the owner." }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMineAsync(1, 9999));
        Assert.Single(_tickets.All.Single().Messages);
    }

    // ---------------- conversation ----------------

    [Fact]
    public async Task TheConversation_IsInOrder_AndSaysWhoWroteEachMessage()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "Which seat would you like?" });
        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "A window seat in row 12, please." });

        var detail = await service.GetMineAsync(1, ticket.Id);

        Assert.Equal(new[] { SupportAuthorType.Customer, SupportAuthorType.Staff, SupportAuthorType.Customer }, detail.Messages.Select(m => m.AuthorType));
        Assert.Equal(new[] { "Ayla Mammadova", "AEROVA Support", "Ayla Mammadova" }, detail.Messages.Select(m => m.AuthorName));
        Assert.True(detail.Messages.Zip(detail.Messages.Skip(1), (a, b) => a.CreatedAt <= b.CreatedAt).All(x => x));
        // The staff member's own name is never shown to the customer.
        Assert.DoesNotContain("Member", string.Join(' ', detail.Messages.Select(m => m.AuthorName)));
    }

    [Fact]
    public async Task ACustomerReply_AsksStaffToLook_ButKeepsARequestThatIsInProgress()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "Please send more details." });
        Assert.Equal(SupportStatus.WaitingForCustomer, _tickets.All[0].Status);

        var after = await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Here are more details about it." });
        Assert.Equal(SupportStatus.Open, after.Status);

        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });
        var again = await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "One more thing about the booking." });
        Assert.Equal(SupportStatus.InProgress, again.Status);
    }

    [Fact]
    public async Task ACustomer_CannotReplyToAResolvedRequest()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.Resolved });

        var detail = await service.GetMineAsync(1, ticket.Id);
        Assert.False(detail.CanReply);
        await Assert.ThrowsAsync<ConflictException>(() => service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Can I still write here?" }));
        Assert.Single(_tickets.All[0].Messages);
    }

    [Fact]
    public async Task AnEmptyOrHugeReply_IsRefused_AndTheSameReplyTwiceIsKeptOnce()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "  " }));
        await Assert.ThrowsAsync<ValidationException>(() => service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = new string('x', 4001) }));

        await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Same words once." });
        await service.ReplyAsync(1, ticket.Id, new SupportReplyRequestDto { Message = "Same words once." });

        Assert.Equal(2, _tickets.All[0].Messages.Count);
        Assert.Equal(2, _alerts.All.Count);
    }

    // ---------------- staff ----------------

    [Fact]
    public async Task AStaffReply_IsSavedAsStaff_SetsTheStatus_AndNotifiesTheCustomerOnce()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());

        var detail = await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "We changed your seat to 12A.", Status = SupportStatus.Resolved });
        await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "We changed your seat to 12A.", Status = SupportStatus.Resolved });

        Assert.Equal(SupportStatus.Resolved, detail.Status);
        Assert.Equal(SupportAuthorType.Staff, detail.Messages[^1].AuthorType);
        var reply = Assert.Single(Notifications, n => n.EmailKey!.Contains(":reply:"));
        Assert.Equal(1, reply.UserId);
        Assert.Contains(ticket.TicketNumber, reply.Title);
        Assert.Contains("We changed your seat to 12A.", reply.Message);
        Assert.Contains("Resolved", reply.Message);
        Assert.Equal(2, _tickets.All[0].Messages.Count);
    }

    [Fact]
    public async Task AStaffReply_WithoutAStatus_WaitsForTheCustomer()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());

        var detail = await service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "Could you tell us the date?" });

        Assert.Equal(SupportStatus.WaitingForCustomer, detail.Status);
    }

    [Fact]
    public async Task ChangingTheStatus_NotifiesTheCustomer_ButTheSameStatusAgainDoesNot()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        var before = Notifications.Count;

        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });
        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });

        Assert.Equal(before + 1, Notifications.Count);
        var notification = Notifications.Last();
        Assert.Contains("In progress", notification.Title);
        Assert.Contains(ticket.TicketNumber, notification.Message);
        Assert.Equal(SupportStatus.InProgress, _tickets.All[0].Status);
    }

    [Fact]
    public async Task StaffCanReopenAResolvedRequest()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());
        await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.Resolved });

        var reopened = await service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });

        Assert.Equal(SupportStatus.InProgress, reopened.Status);
        Assert.True((await service.GetMineAsync(1, ticket.Id)).CanReply);
    }

    [Fact]
    public async Task AnInvalidStatus_OrAnUnknownRequest_IsRefused()
    {
        var service = Create();
        var ticket = await service.CreateAsync(1, Request());

        await Assert.ThrowsAsync<ValidationException>(() => service.SetStatusAsync(ticket.Id, new SupportStatusRequestDto { Status = (SupportStatus)99 }));
        await Assert.ThrowsAsync<ValidationException>(() => service.ReplyAsync(9, ticket.Id, new AdminSupportReplyRequestDto { Message = "x", Status = (SupportStatus)99 }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetStatusAsync(9999, new SupportStatusRequestDto { Status = SupportStatus.Resolved }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReplyAsync(9, 9999, new AdminSupportReplyRequestDto { Message = "Hello" }));
        await Assert.ThrowsAsync<NotFoundException>(() => ((IAdminSupportService)service).GetAsync(9999));
    }

    [Fact]
    public async Task TheStaffList_SearchesFiltersAndPages()
    {
        var service = Create();
        var seat = await service.CreateAsync(1, Request(subject: "Seat change", pnr: "AB12CD"));
        await service.CreateAsync(2, Request(subject: "Refund not received", pnr: "ZZ99YY", category: SupportCategory.CancellationRefund));
        await service.CreateAsync(1, Request(subject: "Cannot log in", pnr: null, category: SupportCategory.Account));
        await service.SetStatusAsync(seat.Id, new SupportStatusRequestDto { Status = SupportStatus.InProgress });

        Assert.Equal(3, (await service.GetAsync(null, null, null, 1, 20)).Total);
        Assert.Equal(new[] { "Refund not received" }, (await service.GetAsync("zz99", null, null, 1, 20)).Items.Select(t => t.Subject));
        Assert.Equal(new[] { "Refund not received" }, (await service.GetAsync("orkhan@", null, null, 1, 20)).Items.Select(t => t.Subject));
        Assert.Equal(2, (await service.GetAsync("ayla@example", null, null, 1, 20)).Total);
        Assert.Equal(new[] { seat.TicketNumber }, (await service.GetAsync(seat.TicketNumber, null, null, 1, 20)).Items.Select(t => t.TicketNumber));
        Assert.Equal(new[] { "Seat change" }, (await service.GetAsync(null, SupportStatus.InProgress, null, 1, 20)).Items.Select(t => t.Subject));
        Assert.Equal(new[] { "Cannot log in" }, (await service.GetAsync(null, null, SupportCategory.Account, 1, 20)).Items.Select(t => t.Subject));
        Assert.Empty((await service.GetAsync(null, SupportStatus.Resolved, SupportCategory.Account, 1, 20)).Items);

        var page = await service.GetAsync(null, null, null, 2, 2);
        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("Ayla Mammadova", (await service.GetAsync(seat.TicketNumber, null, null, 1, 20)).Items[0].CustomerName);
    }

    [Fact]
    public void TheContactEmail_IsShownOnlyWhenConfigured()
    {
        Assert.Null(Create().GetInfo().ContactEmail);
        Assert.Null(Create(new SupportSettings { ContactEmail = "  " }).GetInfo().ContactEmail);
        Assert.Equal("help@aerova.test", Create(new SupportSettings { ContactEmail = "help@aerova.test " }).GetInfo().ContactEmail);
    }
}
