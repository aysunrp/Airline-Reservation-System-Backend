using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Support;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A test API with a support contact address and an admin address, so the Support page info and the admin alert can be checked.</summary>
public sealed class SupportApiFactory : ApiFactory
{
    public SupportApiFactory()
    {
        SupportContactEmail = "help@aerova.test";
        AdminAlertRecipient = "admin@aerova.test";
    }
}

/// <summary>A test API where each customer may send three support requests or replies a minute.</summary>
public sealed class SupportLimitedApiFactory : ApiFactory
{
    public SupportLimitedApiFactory()
    {
        SupportRateLimitPerMinute = 3;
    }
}

/// <summary>
/// The support system over HTTP, as the Support page and the admin page call it. The mail server is a stand-in: e-mails are checked in its list,
/// and no real e-mail is sent.
/// </summary>
public class SupportEndpointTests : IClassFixture<SupportApiFactory>, IClassFixture<SupportLimitedApiFactory>
{
    private readonly SupportApiFactory _factory;
    private readonly SupportLimitedApiFactory _limited;

    public SupportEndpointTests(SupportApiFactory factory, SupportLimitedApiFactory limited)
    {
        _factory = factory;
        _limited = limited;
        _factory.Emails.Fail = false;
        _factory.Emails.IsConfigured = true;
    }

    private async Task<(HttpClient Client, string Email)> CustomerAsync(ApiFactory? factory = null)
    {
        factory ??= _factory;
        var email = UniqueEmail("support");
        var client = factory.CreateClient();
        client.WithToken((await client.RegisterAsync(email)).AccessToken);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailVerifiedAt, DateTime.UtcNow));
        return (client, email);
    }

    private static object NewTicket(string subject = "Seat change", string? pnr = "AB12CD") => new
    {
        category = "Booking", subject, message = "I need help with my seat selection, please.", bookingPnr = pnr
    };

    private static async Task<SupportTicketDetailDto> OpenAsync(HttpClient client, string subject = "Seat change")
    {
        var response = await client.PostAsJsonAsync("/api/support/tickets", NewTicket(subject), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<SupportTicketDetailDto>();
    }

    private async Task SendJobsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync();
        await scope.ServiceProvider.GetRequiredService<IAdminAlertService>().SendDueAsync();
    }

    // ---------------- who may do what ----------------

    [Fact]
    public async Task AVisitorWhoIsNotSignedIn_CannotUseTheTicketEndpoints()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/support/tickets", NewTicket(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/support/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/support/tickets/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/support/tickets/1/replies", new { message = "hello there" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/support/tickets")).StatusCode);
    }

    [Fact]
    public async Task ACustomer_CannotUseTheAdminEndpoints_WhateverTheyEditInTheBrowser()
    {
        var (customer, _) = await CustomerAsync();
        var ticket = await OpenAsync(customer);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/support/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/support/tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/admin/support/tickets/{ticket.Id}/replies", new { message = "I am staff now" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync($"/api/admin/support/tickets/{ticket.Id}/status", new { status = "Resolved" }, Json)).StatusCode);

        // A forged user id in the body or the query changes nothing: the owner is the signed-in account.
        var forged = await customer.PostAsJsonAsync("/api/support/tickets", new { category = "Other", subject = "Forged", message = "Trying to act as someone else.", userId = 1 }, Json);
        Assert.Equal(HttpStatusCode.Created, forged.StatusCode);
        Assert.Equal(2, (await (await customer.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>()).Count);
    }

    [Fact]
    public async Task ACustomer_CannotReadOrAnswerAnotherCustomersRequest()
    {
        var (owner, _) = await CustomerAsync();
        var (other, _) = await CustomerAsync();
        var ticket = await OpenAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/support/tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/support/tickets/{ticket.Id}/replies", new { message = "Not my ticket at all." }, Json)).StatusCode);
        Assert.Empty(await (await other.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>());
        Assert.Single((await (await owner.GetAsync($"/api/support/tickets/{ticket.Id}")).ReadAsync<SupportTicketDetailDto>()).Messages);
    }

    // ---------------- the whole conversation ----------------

    [Fact]
    public async Task OpenReplyChangeStatus_TheWholeConversation_WithEmails()
    {
        var (customer, email) = await CustomerAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var created = await OpenAsync(customer);
        Assert.Matches("^SUP-[A-Z2-9]{6}$", created.TicketNumber);
        Assert.Equal("AB12CD", created.BookingPnr);

        var mine = await (await customer.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>();
        Assert.Equal(created.TicketNumber, Assert.Single(mine).TicketNumber);

        // Staff finds it by number, e-mail, subject and booking reference.
        foreach (var term in new[] { created.TicketNumber, email, "Seat change", "AB12CD" })
        {
            var page = await (await admin.GetAsync($"/api/admin/support/tickets?search={Uri.EscapeDataString(term)}")).ReadAsync<AdminSupportPageDto>();
            Assert.Contains(page.Items, t => t.Id == created.Id && t.CustomerEmail == email);
        }

        var reply = await admin.PostAsJsonAsync($"/api/admin/support/tickets/{created.Id}/replies", new { message = "Which seat would you like?", status = "InProgress" }, Json);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.Equal(SupportStatus.InProgress, (await reply.ReadAsync<AdminSupportTicketDetailDto>()).Status);

        var seen = await (await customer.GetAsync($"/api/support/tickets/{created.Id}")).ReadAsync<SupportTicketDetailDto>();
        Assert.Equal(new[] { SupportAuthorType.Customer, SupportAuthorType.Staff }, seen.Messages.Select(m => m.AuthorType));
        Assert.Equal("AEROVA Support", seen.Messages[1].AuthorName);
        Assert.True(seen.CanReply);

        var answered = await customer.PostAsJsonAsync($"/api/support/tickets/{created.Id}/replies", new { message = "A window seat in row 12, please." }, Json);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Equal(3, (await answered.ReadAsync<SupportTicketDetailDto>()).Messages.Count);

        var resolved = await admin.PutAsJsonAsync($"/api/admin/support/tickets/{created.Id}/status", new { status = "Resolved" }, Json);
        Assert.Equal(SupportStatus.Resolved, (await resolved.ReadAsync<AdminSupportTicketDetailDto>()).Status);

        // A resolved request takes no more customer replies.
        var late = await customer.PostAsJsonAsync($"/api/support/tickets/{created.Id}/replies", new { message = "One more question please." }, Json);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.False((await (await customer.GetAsync($"/api/support/tickets/{created.Id}")).ReadAsync<SupportTicketDetailDto>()).CanReply);

        // Emails: sent by the notification job, to the customer's own address, with the ticket number and a link to the Support page.
        await SendJobsAsync();
        var toCustomer = _factory.Emails.Sent.Where(m => m.To == email && m.TextBody.Contains("SUP-")).ToList();
        Assert.Contains(toCustomer, m => m.TextBody.Contains(created.TicketNumber) && m.TextBody.Contains("received"));
        Assert.Contains(toCustomer, m => m.TextBody.Contains("Which seat would you like?") && m.TextBody.Contains("In progress"));
        Assert.Contains(toCustomer, m => m.TextBody.Contains("Resolved"));
        Assert.All(toCustomer, m => Assert.Contains("https://aerova.test/support.html", m.TextBody));

        // The admin is told about the new request and the customer's reply, not about their own replies.
        var toAdmin = _factory.Emails.Sent.Where(m => m.To == "admin@aerova.test" && m.TextBody.Contains(created.TicketNumber)).ToList();
        Assert.Contains(toAdmin, m => m.Subject.Contains("New support request"));
        Assert.Contains(toAdmin, m => m.Subject.Contains("Customer reply"));

        // Running the jobs again sends nothing twice.
        var before = _factory.Emails.Sent.Count;
        await SendJobsAsync();
        Assert.Equal(before, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task WhenTheMailServerFails_TheTicketIsStillCreated_AndTheEmailIsSentLater()
    {
        var (customer, email) = await CustomerAsync();
        _factory.Emails.Fail = true;

        var created = await OpenAsync(customer);
        await SendJobsAsync();
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == email && m.TextBody.Contains("SUP-"));
        Assert.Single(await (await customer.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>());

        _factory.Emails.Fail = false;
        await SendJobsAsync();
        Assert.Contains(_factory.Emails.Sent, m => m.To == email && m.TextBody.Contains(created.TicketNumber));
    }

    [Fact]
    public async Task TheSameRequestSentTwice_IsOneTicketAndOneEmail()
    {
        var (customer, email) = await CustomerAsync();

        var first = await OpenAsync(customer, "Double click");
        var second = await OpenAsync(customer, "Double click");
        await SendJobsAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await (await customer.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>());
        Assert.Single(_factory.Emails.Sent.Where(m => m.To == email && m.TextBody.Contains("SUP-")));
    }

    // ---------------- validation, filters, limits ----------------

    [Theory]
    [InlineData("", "I need help with my seat selection, please.", "AB12CD", "Booking")]
    [InlineData("Seat", "short", "AB12CD", "Booking")]
    [InlineData("Seat", "I need help with my seat selection, please.", "not a pnr!", "Booking")]
    [InlineData("Seat", "I need help with my seat selection, please.", "AB12CD", "Nonsense")]
    public async Task InvalidRequests_Are400(string subject, string message, string pnr, string category)
    {
        var (customer, _) = await CustomerAsync();

        var response = await customer.PostAsJsonAsync("/api/support/tickets", new { category, subject, message, bookingPnr = pnr }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await (await customer.GetAsync("/api/support/tickets")).ReadAsync<List<SupportTicketDto>>());
    }

    [Fact]
    public async Task TheStaffList_FiltersByStatusAndCategory_AndPages()
    {
        var (customer, email) = await CustomerAsync();
        var admin = await _factory.CreateAdminClientAsync();
        await OpenAsync(customer, "First problem");
        var second = await customer.PostAsJsonAsync("/api/support/tickets", new { category = "Baggage", subject = "Lost bag", message = "My bag did not arrive at all." }, Json);
        var secondTicket = await second.ReadAsync<SupportTicketDetailDto>();
        await admin.PutAsJsonAsync($"/api/admin/support/tickets/{secondTicket.Id}/status", new { status = "Resolved" }, Json);

        var baggage = await (await admin.GetAsync($"/api/admin/support/tickets?search={Uri.EscapeDataString(email)}&category=Baggage")).ReadAsync<AdminSupportPageDto>();
        Assert.Equal(new[] { "Lost bag" }, baggage.Items.Select(t => t.Subject));
        var resolved = await (await admin.GetAsync($"/api/admin/support/tickets?search={Uri.EscapeDataString(email)}&status=Resolved")).ReadAsync<AdminSupportPageDto>();
        Assert.Equal(new[] { "Lost bag" }, resolved.Items.Select(t => t.Subject));
        var paged = await (await admin.GetAsync($"/api/admin/support/tickets?search={Uri.EscapeDataString(email)}&page=2&pageSize=1")).ReadAsync<AdminSupportPageDto>();
        Assert.Equal(2, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/support/tickets?status=Nonsense")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/support/tickets/999999")).StatusCode);
    }

    [Fact]
    public async Task TheSupportPageInfo_ShowsOnlyTheConfiguredContactAddress()
    {
        var configured = await (await _factory.CreateClient().GetAsync("/api/support/info")).ReadAsync<SupportInfoDto>();
        var unconfigured = await (await _limited.CreateClient().GetAsync("/api/support/info")).ReadAsync<SupportInfoDto>();

        Assert.Equal("help@aerova.test", configured.ContactEmail);
        Assert.Null(unconfigured.ContactEmail);
    }

    [Fact]
    public async Task RepeatedRequests_AreLimitedPerCustomer()
    {
        var (customer, _) = await CustomerAsync(_limited);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync("/api/support/tickets", NewTicket($"Request number {i}"), Json)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await customer.PostAsJsonAsync("/api/support/tickets", NewTicket("One request too many"), Json)).StatusCode);

        // Another customer from the same address is not blocked by it.
        var (other, _) = await CustomerAsync(_limited);
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsJsonAsync("/api/support/tickets", NewTicket("Mine"), Json)).StatusCode);
    }
}
