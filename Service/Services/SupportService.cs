using System.Security.Cryptography;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Support;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Support requests. Whose ticket it is comes from the signed-in account only, and every customer call checks the owner before it reads or
/// writes anything. E-mails are not sent here: a notification is saved with the ticket (once per event, by key) and the existing notification
/// job e-mails it with retries, so a mail server that is down never loses or blocks a ticket. New tickets also queue a message for the admin.
/// </summary>
public class SupportService : ISupportService, IAdminSupportService
{
    private const string StaffName = "AEROVA Support";
    private const string NumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly ISupportTicketRepository _tickets;
    private readonly IUserRepository _users;
    private readonly INotificationService _notifications;
    private readonly IAdminAlertService _adminAlerts;
    private readonly IValidator<CreateSupportTicketRequestDto> _createValidator;
    private readonly IValidator<SupportReplyRequestDto> _replyValidator;
    private readonly IValidator<AdminSupportReplyRequestDto> _adminReplyValidator;
    private readonly IValidator<SupportStatusRequestDto> _statusValidator;
    private readonly SupportSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ISupportRealtimePublisher? _realtime;
    private readonly ILogger<SupportService>? _logger;

    public SupportService(
        ISupportTicketRepository tickets,
        IUserRepository users,
        INotificationService notifications,
        IAdminAlertService adminAlerts,
        IValidator<CreateSupportTicketRequestDto> createValidator,
        IValidator<SupportReplyRequestDto> replyValidator,
        IValidator<AdminSupportReplyRequestDto> adminReplyValidator,
        IValidator<SupportStatusRequestDto> statusValidator,
        IOptions<SupportSettings> settings,
        TimeProvider timeProvider,
        ISupportRealtimePublisher? realtime = null,
        ILogger<SupportService>? logger = null)
    {
        _realtime = realtime;
        _logger = logger;
        _tickets = tickets;
        _users = users;
        _notifications = notifications;
        _adminAlerts = adminAlerts;
        _createValidator = createValidator;
        _replyValidator = replyValidator;
        _adminReplyValidator = adminReplyValidator;
        _statusValidator = statusValidator;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    public SupportInfoDto GetInfo()
        => new(string.IsNullOrWhiteSpace(_settings.ContactEmail) ? null : _settings.ContactEmail.Trim());

    // ---------------- customer ----------------

    public async Task<SupportTicketDetailDto> CreateAsync(int userId, CreateSupportTicketRequestDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new UnauthorizedException("Zəhmət olmasa, yenidən daxil olun.");
        var now = Now();
        var subject = request.Subject.Trim();
        var message = request.Message.Trim();

        // The same request sent again a moment later (a double click, a retry) is the same request: no second ticket, no second e-mail.
        var duplicate = await _tickets.FindRecentDuplicateAsync(userId, subject, now.AddSeconds(-Math.Max(0, _settings.DuplicateWindowSeconds)), cancellationToken);
        if (duplicate is not null && duplicate.Messages.Count > 0 && duplicate.Messages.OrderBy(m => m.Id).First().Body == message && duplicate.Category == request.Category)
        {
            return await DetailAsync(userId, duplicate.Id, cancellationToken);
        }

        if (await _tickets.CountOpenForUserAsync(userId, cancellationToken) >= Math.Max(1, _settings.MaxOpenTicketsPerUser))
        {
            throw new BusinessRuleException("Çox sayda açıq dəstək sorğunuz var. Bəzilərinin həllini gözləyin.", "too_many_open_tickets");
        }

        var ticket = new SupportTicket
        {
            TicketNumber = await NewNumberAsync(cancellationToken),
            UserId = userId,
            Category = request.Category,
            Subject = subject,
            BookingPnr = string.IsNullOrWhiteSpace(request.BookingPnr) ? null : request.BookingPnr.Trim().ToUpperInvariant(),
            Status = SupportStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
            LastMessageAt = now
        };
        ticket.Messages.Add(new SupportMessage { AuthorType = SupportAuthorType.Customer, AuthorUserId = userId, Body = message, CreatedAt = now });

        await _tickets.AddAsync(ticket, cancellationToken);
        await _tickets.SaveChangesAsync(cancellationToken);

        // Saved after the ticket exists: a failure here can not undo it (the notification and the admin alert are best effort and retried by the jobs).
        await _notifications.QueueOnceAsync($"support:{ticket.Id}:created", userId, null, NotificationType.SupportUpdate,
            $"Support request {ticket.TicketNumber} received", $"We received your request \"{Shorten(subject, 80)}\" ({ticket.TicketNumber}). We will answer in your Support page.", cancellationToken);
        await _adminAlerts.QueueAsync($"support:{ticket.Id}:created", $"New support request {ticket.TicketNumber}",
            $"{user.FirstName} {user.LastName} ({user.Email}) opened a {request.Category} request: \"{Shorten(subject, 100)}\"" +
            (ticket.BookingPnr is null ? string.Empty : $" (booking {ticket.BookingPnr})") + ".", cancellationToken);
        await QueueStaffNotificationsAsync($"support:{ticket.Id}:staff:{{0}}:new", $"New support request {ticket.TicketNumber}",
            $"{Shorten(subject, 100)} ({request.Category})", cancellationToken);
        await SaveQuietlyAsync(cancellationToken);

        // Published only now that the ticket and its first message are saved.
        await PublishToStaffAsync("ticketCreated", ticket, withDetails: true, cancellationToken);

        return ToDetail(ticket, user, includeCanReply: true);
    }

    public async Task<IReadOnlyList<SupportTicketDto>> GetMineAsync(int userId, CancellationToken cancellationToken = default)
        => (await _tickets.GetForUserAsync(userId, cancellationToken)).Select(r => ToDto(r.Ticket, r.MessageCount)).ToList();

    public Task<SupportTicketDetailDto> GetMineAsync(int userId, int ticketId, CancellationToken cancellationToken = default)
        => DetailAsync(userId, ticketId, cancellationToken);

    public async Task<SupportTicketDetailDto> ReplyAsync(int userId, int ticketId, SupportReplyRequestDto request, CancellationToken cancellationToken = default)
    {
        await _replyValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await GetOwnedAsync(userId, ticketId, cancellationToken);

        if (ticket.Status == SupportStatus.Resolved)
        {
            throw new ConflictException("Bu sorğu həll olunub. Hələ də köməyə ehtiyacınız varsa, yeni sorğu açın.");
        }

        var now = Now();
        var body = request.Message.Trim();
        var last = ticket.Messages.OrderByDescending(m => m.Id).FirstOrDefault();
        if (last is not null && last.AuthorType == SupportAuthorType.Customer && last.Body == body
            && now - last.CreatedAt < TimeSpan.FromSeconds(Math.Max(0, _settings.DuplicateWindowSeconds)))
        {
            // The same reply sent twice: kept once.
            return ToDetail(ticket, ticket.User, includeCanReply: true);
        }

        var message = new SupportMessage { TicketId = ticket.Id, AuthorType = SupportAuthorType.Customer, AuthorUserId = userId, Body = body, CreatedAt = now };
        ticket.Messages.Add(message);
        // The customer wrote again: staff has to look (a request staff is already working on stays in progress).
        ticket.Status = ticket.Status == SupportStatus.WaitingForCustomer ? SupportStatus.Open : ticket.Status;
        ticket.LastMessageAt = now;
        ticket.UpdatedAt = now;
        await _tickets.SaveChangesAsync(cancellationToken);

        await _adminAlerts.QueueAsync($"support:{ticket.Id}:customer-reply:{message.Id}", $"Customer reply on {ticket.TicketNumber}",
            $"{ticket.User.FirstName} {ticket.User.LastName} replied on \"{Shorten(ticket.Subject, 100)}\".", cancellationToken);
        await QueueStaffNotificationsAsync($"support:{ticket.Id}:staff:{{0}}:reply:{message.Id}", $"Customer reply on {ticket.TicketNumber}",
            Shorten(ticket.Subject, 100), cancellationToken);
        await SaveQuietlyAsync(cancellationToken);

        await PublishToStaffAsync("customerReplied", ticket, withDetails: true, cancellationToken);

        return ToDetail(ticket, ticket.User, includeCanReply: true);
    }

    // ---------------- staff ----------------

    public async Task<AdminSupportPageDto> GetAsync(string? search, SupportStatus? status, SupportCategory? category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _tickets.SearchAsync(search, status, category, page, pageSize, cancellationToken);
        return new AdminSupportPageDto(items.Select(r => new AdminSupportTicketDto(r.Ticket.Id, r.Ticket.TicketNumber, r.Ticket.Subject, r.Ticket.Category, r.Ticket.Status,
            r.Ticket.BookingPnr, $"{r.Ticket.User.FirstName} {r.Ticket.User.LastName}".Trim(), r.Ticket.User.Email, r.MessageCount, r.Ticket.CreatedAt, r.Ticket.UpdatedAt ?? r.Ticket.CreatedAt)).ToList(),
            total, page, pageSize);
    }

    async Task<AdminSupportTicketDetailDto> IAdminSupportService.GetAsync(int ticketId, CancellationToken cancellationToken)
        => ToAdminDetail(await _tickets.GetDetailsAsync(ticketId, cancellationToken) ?? throw new NotFoundException("Dəstək sorğusu tapılmadı."));

    public async Task<AdminSupportTicketDetailDto> ReplyAsync(int staffUserId, int ticketId, AdminSupportReplyRequestDto request, CancellationToken cancellationToken = default)
    {
        await _adminReplyValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await _tickets.GetDetailsAsync(ticketId, cancellationToken) ?? throw new NotFoundException("Dəstək sorğusu tapılmadı.");

        var now = Now();
        var body = request.Message.Trim();
        var last = ticket.Messages.OrderByDescending(m => m.Id).FirstOrDefault();
        if (last is not null && last.AuthorType == SupportAuthorType.Staff && last.Body == body && last.AuthorUserId == staffUserId
            && now - last.CreatedAt < TimeSpan.FromSeconds(Math.Max(0, _settings.DuplicateWindowSeconds)))
        {
            return ToAdminDetail(ticket);
        }

        var message = new SupportMessage { TicketId = ticket.Id, AuthorType = SupportAuthorType.Staff, AuthorUserId = staffUserId, Body = body, CreatedAt = now };
        ticket.Messages.Add(message);
        ticket.Status = request.Status ?? SupportStatus.WaitingForCustomer;
        ticket.LastMessageAt = now;
        ticket.UpdatedAt = now;
        await _tickets.SaveChangesAsync(cancellationToken);

        // One notification per reply: it also names the status the request has now.
        await _notifications.QueueOnceAsync($"support:{ticket.Id}:reply:{message.Id}", ticket.UserId, null, NotificationType.SupportUpdate,
            $"New reply on support request {ticket.TicketNumber}",
            $"Support replied to \"{Shorten(ticket.Subject, 80)}\" ({ticket.TicketNumber}): {Shorten(body, 400)} Status: {StatusText(ticket.Status)}.", cancellationToken);
        await SaveQuietlyAsync(cancellationToken);

        await PublishToCustomerAsync("staffReplied", ticket, cancellationToken);
        await PublishToStaffAsync("ticketUpdated", ticket, withDetails: true, cancellationToken);

        return ToAdminDetail(ticket);
    }

    public async Task<AdminSupportTicketDetailDto> SetStatusAsync(int ticketId, SupportStatusRequestDto request, CancellationToken cancellationToken = default)
    {
        await _statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await _tickets.GetDetailsAsync(ticketId, cancellationToken) ?? throw new NotFoundException("Dəstək sorğusu tapılmadı.");

        // The same status again changes nothing and tells nobody.
        if (ticket.Status != request.Status)
        {
            var now = Now();
            ticket.Status = request.Status;
            ticket.UpdatedAt = now;
            await _tickets.SaveChangesAsync(cancellationToken);

            await _notifications.QueueOnceAsync($"support:{ticket.Id}:status:{now.Ticks}", ticket.UserId, null, NotificationType.SupportUpdate,
                $"Support request {ticket.TicketNumber} is now {StatusText(ticket.Status)}",
                $"The status of your request \"{Shorten(ticket.Subject, 80)}\" ({ticket.TicketNumber}) is now: {StatusText(ticket.Status)}.", cancellationToken);
            await SaveQuietlyAsync(cancellationToken);

            await PublishToCustomerAsync("statusChanged", ticket, cancellationToken);
            await PublishToStaffAsync("ticketUpdated", ticket, withDetails: true, cancellationToken);
        }

        return ToAdminDetail(ticket);
    }

    // ---------------- helpers ----------------

    private async Task<SupportTicket> GetOwnedAsync(int userId, int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await _tickets.GetDetailsAsync(ticketId, cancellationToken);
        // Somebody else's ticket looks exactly like one that does not exist.
        return ticket is not null && ticket.UserId == userId ? ticket : throw new NotFoundException("Dəstək sorğusu tapılmadı.");
    }

    private async Task<SupportTicketDetailDto> DetailAsync(int userId, int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await GetOwnedAsync(userId, ticketId, cancellationToken);
        return ToDetail(ticket, ticket.User, includeCanReply: true);
    }

    private async Task<string> NewNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[6];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = NumberAlphabet[RandomNumberGenerator.GetInt32(NumberAlphabet.Length)];
            }

            var number = "SUP-" + new string(chars);
            if (!await _tickets.NumberExistsAsync(number, cancellationToken))
            {
                return number;
            }
        }

        throw new ServiceUnavailableException("Sorğunu hazırda yaratmaq mümkün olmadı. Yenidən cəhd edin.");
    }

    /// <summary>
    /// A notification for every active staff account (the bell of the admin page), once per event and account. Staff notifications are
    /// never e-mailed: the admin e-mail is the separate alert.
    /// </summary>
    private async Task QueueStaffNotificationsAsync(string keyFormat, string title, string message, CancellationToken cancellationToken)
    {
        foreach (var staffId in await _users.GetActiveStaffIdsAsync(cancellationToken))
        {
            await _notifications.QueueOnceAsync(string.Format(keyFormat, staffId), staffId, null, NotificationType.SupportStaff, title, message, cancellationToken);
        }
    }

    private SupportRealtimeEvent Event(string type, SupportTicket ticket, bool withDetails)
        => new(Guid.NewGuid(), type, ticket.Id, ticket.TicketNumber, ticket.Status.ToString(), withDetails ? ticket.Subject : null,
            withDetails ? ticket.Category.ToString() : null, Now());

    /// <summary>Real-time events are a convenience on top of the saved ticket: a failure here never loses or undoes anything.</summary>
    private async Task PublishToStaffAsync(string type, SupportTicket ticket, bool withDetails, CancellationToken cancellationToken)
    {
        if (_realtime is null) return;
        try { await _realtime.ToStaffAsync(Event(type, ticket, withDetails), cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException) { _logger?.LogWarning("A real-time support event could not be published ({ErrorType}).", ex.GetType().Name); }
    }

    private async Task PublishToCustomerAsync(string type, SupportTicket ticket, CancellationToken cancellationToken)
    {
        if (_realtime is null) return;
        try { await _realtime.ToCustomerAsync(ticket.UserId, Event(type, ticket, withDetails: false), cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException) { _logger?.LogWarning("A real-time support event could not be published ({ErrorType}).", ex.GetType().Name); }
    }

    /// <summary>Saves the notification and the admin alert that were added after the ticket; they can not undo the ticket, which is already saved.</summary>
    private async Task SaveQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _tickets.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A retried request stored the same notification first (unique key): nothing is lost.
        }
    }

    private static string Shorten(string text, int length) => text.Length > length ? text[..length] + "…" : text;

    private static string StatusText(SupportStatus status) => status switch
    {
        SupportStatus.InProgress => "In progress",
        SupportStatus.WaitingForCustomer => "Waiting for your reply",
        SupportStatus.Resolved => "Resolved",
        _ => "Open"
    };

    private static SupportTicketDto ToDto(SupportTicket t, int messageCount)
        => new(t.Id, t.TicketNumber, t.Subject, t.Category, t.Status, t.BookingPnr, messageCount, t.CreatedAt, t.UpdatedAt ?? t.CreatedAt);

    private static SupportMessageDto ToDto(SupportMessage m, User customer)
        => new(m.Id, m.AuthorType, m.AuthorType == SupportAuthorType.Staff ? StaffName : $"{customer.FirstName} {customer.LastName}".Trim(), m.Body, m.CreatedAt);

    private static SupportTicketDetailDto ToDetail(SupportTicket t, User customer, bool includeCanReply)
        => new(t.Id, t.TicketNumber, t.Subject, t.Category, t.Status, t.BookingPnr, t.CreatedAt, t.UpdatedAt ?? t.CreatedAt,
            includeCanReply && t.Status != SupportStatus.Resolved, t.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => ToDto(m, customer)).ToList());

    private static AdminSupportTicketDetailDto ToAdminDetail(SupportTicket t)
        => new(t.Id, t.TicketNumber, t.Subject, t.Category, t.Status, t.BookingPnr, $"{t.User.FirstName} {t.User.LastName}".Trim(), t.User.Email,
            t.CreatedAt, t.UpdatedAt ?? t.CreatedAt, t.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => ToDto(m, t.User)).ToList());
}
