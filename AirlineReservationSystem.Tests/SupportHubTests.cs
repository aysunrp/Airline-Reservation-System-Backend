using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AirlineReservationSystem.Hubs;
using Domain.Enums;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Support;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// The real-time channel over a real SignalR hub (long polling through the in-memory test server). These tests prove the routing and the
/// authorization of the hub itself. They are automated tests; a real browser with a WebSocket is a separate check.
/// </summary>
public class SupportHubTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportHubTests(SupportApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.Fail = false;
        _factory.Emails.IsConfigured = true;
    }

    private sealed class Listener : IAsyncDisposable
    {
        public HubConnection Connection { get; init; } = null!;
        public List<JsonElement> Events { get; } = new();
        private readonly object _lock = new();

        public void Add(JsonElement e) { lock (_lock) { Events.Add(e); } }

        public IReadOnlyList<JsonElement> Snapshot() { lock (_lock) { return Events.ToList(); } }

        public async Task<JsonElement> WaitForAsync(string type, int ticketId, int seconds = 6)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until)
            {
                var found = Snapshot().Where(e => e.GetProperty("type").GetString() == type && e.GetProperty("ticketId").GetInt32() == ticketId).ToList();
                if (found.Count > 0) return found[0];
                await Task.Delay(50);
            }

            throw new TimeoutException($"No {type} event for ticket {ticketId} arrived. Got: {string.Join(", ", Snapshot().Select(e => e.GetRawText()))}");
        }

        public async Task<bool> NothingArrivesAsync(int milliseconds = 700)
        {
            await Task.Delay(milliseconds);
            return Snapshot().Count == 0;
        }

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    private HubConnection Build(string? token)
        => new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, SupportHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();

    private async Task<Listener> ConnectAsync(string token)
    {
        var listener = new Listener { Connection = Build(token) };
        listener.Connection.On<JsonElement>(SupportHub.EventName, listener.Add);
        await listener.Connection.StartAsync();
        return listener;
    }

    private static string TokenOf(HttpClient client) => client.DefaultRequestHeaders.Authorization!.Parameter!;

    private async Task<(HttpClient Client, string Email)> CustomerAsync()
    {
        var email = UniqueEmail("hub");
        var client = _factory.CreateClient();
        client.WithToken((await client.RegisterAsync(email)).AccessToken);
        return (client, email);
    }

    private static async Task<SupportTicketDetailDto> OpenAsync(HttpClient client, string subject = "Seat change")
    {
        var response = await client.PostAsJsonAsync("/api/support/tickets",
            new { category = "Booking", subject, message = "I need help with my seat selection, please.", bookingPnr = "AB12CD" }, Json);
        return await response.ReadAsync<SupportTicketDetailDto>();
    }

    // ---------------- who may connect ----------------

    [Fact]
    public async Task AConnectionWithoutASignIn_OrWithAForgedToken_IsRefused()
    {
        await using var anonymous = new Listener { Connection = Build(null) };
        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.Connection.StartAsync());

        var (customer, _) = await CustomerAsync();
        var forged = TokenOf(customer)[..^4] + "AAAA";
        await using var tampered = new Listener { Connection = Build(forged) };
        await Assert.ThrowsAnyAsync<Exception>(() => tampered.Connection.StartAsync());

        await using var garbage = new Listener { Connection = Build("not-a-token") };
        await Assert.ThrowsAnyAsync<Exception>(() => garbage.Connection.StartAsync());
    }

    [Fact]
    public void TheHub_HasNoMethodABrowserCanCall_SoNobodyCanJoinAGroupOrAskForAnotherCustomersEvents()
    {
        var callable = typeof(SupportHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetBaseDefinition().DeclaringType == typeof(SupportHub));

        Assert.Empty(callable);
    }

    // ---------------- who receives what ----------------

    [Fact]
    public async Task ANewRequest_ReachesStaffOnly_WithoutTheCustomersEmailOrTheMessage()
    {
        var staff = await _factory.CreateAccountAsync(UserRole.Admin);
        var (customer, email) = await CustomerAsync();
        var (other, _) = await CustomerAsync();
        await using var staffListener = await ConnectAsync(staff.Token);
        await using var customerListener = await ConnectAsync(TokenOf(customer));
        await using var otherListener = await ConnectAsync(TokenOf(other));

        var ticket = await OpenAsync(customer);

        var received = await staffListener.WaitForAsync("ticketCreated", ticket.Id);
        Assert.Equal(ticket.TicketNumber, received.GetProperty("ticketNumber").GetString());
        Assert.Equal("Seat change", received.GetProperty("subject").GetString());
        Assert.Equal("Booking", received.GetProperty("category").GetString());
        Assert.Equal("Open", received.GetProperty("status").GetString());
        var raw = received.GetRawText();
        Assert.DoesNotContain(email, raw);
        Assert.DoesNotContain("seat selection", raw);
        Assert.True(await customerListener.NothingArrivesAsync(), "the customer who opened it gets no staff event");
        Assert.True(await otherListener.NothingArrivesAsync(), "another customer gets nothing");
    }

    [Fact]
    public async Task AStaffReply_ReachesTheOwnerOnly_AndEachEventIsNewAndHasItsOwnId()
    {
        var staff = await _factory.CreateAccountAsync(UserRole.Admin);
        var (owner, _) = await CustomerAsync();
        var (other, _) = await CustomerAsync();
        var ticket = await OpenAsync(owner);
        await using var staffListener = await ConnectAsync(staff.Token);
        await using var ownerListener = await ConnectAsync(TokenOf(owner));
        await using var otherListener = await ConnectAsync(TokenOf(other));

        await staff.Client.PostAsJsonAsync($"/api/admin/support/tickets/{ticket.Id}/replies", new { message = "Which seat would you like?", status = "InProgress" }, Json);

        var toOwner = await ownerListener.WaitForAsync("staffReplied", ticket.Id);
        Assert.Equal("InProgress", toOwner.GetProperty("status").GetString());
        Assert.DoesNotContain("Which seat", toOwner.GetRawText());
        Assert.False(toOwner.TryGetProperty("subject", out var subject) && subject.ValueKind == JsonValueKind.String, "the owner's event carries no subject");
        await staffListener.WaitForAsync("ticketUpdated", ticket.Id);
        Assert.True(await otherListener.NothingArrivesAsync(), "another customer gets nothing about this ticket");

        // A status change reaches the owner; the same status again reaches nobody.
        await staff.Client.PutAsJsonAsync($"/api/admin/support/tickets/{ticket.Id}/status", new { status = "Resolved" }, Json);
        await ownerListener.WaitForAsync("statusChanged", ticket.Id);
        var before = ownerListener.Snapshot().Count;
        await staff.Client.PutAsJsonAsync($"/api/admin/support/tickets/{ticket.Id}/status", new { status = "Resolved" }, Json);
        await Task.Delay(600);
        Assert.Equal(before, ownerListener.Snapshot().Count);

        var ids = ownerListener.Snapshot().Select(e => e.GetProperty("eventId").GetGuid()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task ACustomerReply_ReachesStaff_AndARepeatedRequestPublishesNothingTwice()
    {
        var staff = await _factory.CreateAccountAsync(UserRole.Admin);
        var (customer, _) = await CustomerAsync();
        await using var staffListener = await ConnectAsync(staff.Token);

        var ticket = await OpenAsync(customer);
        var again = await OpenAsync(customer);
        await staffListener.WaitForAsync("ticketCreated", ticket.Id);
        Assert.Equal(ticket.Id, again.Id);

        await customer.PostAsJsonAsync($"/api/support/tickets/{ticket.Id}/replies", new { message = "Here are more details about it." }, Json);
        await staffListener.WaitForAsync("customerReplied", ticket.Id);

        await Task.Delay(600);
        Assert.Single(staffListener.Snapshot(), e => e.GetProperty("type").GetString() == "ticketCreated");
    }

    // ---------------- the persisted bell ----------------

    [Fact]
    public async Task TheStaffBell_IsPersisted_PrivateToStaff_AndNeverEmailed()
    {
        var staff = await _factory.CreateAccountAsync(UserRole.Admin);
        var (customer, _) = await CustomerAsync();
        var unreadBefore = await (await staff.Client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>();

        var ticket = await OpenAsync(customer);

        var staffList = await (await staff.Client.GetAsync("/api/notifications")).ReadJsonAsync();
        Assert.Contains(staffList.EnumerateArray(), n => n.GetProperty("title").GetString()!.Contains(ticket.TicketNumber));
        Assert.Equal(unreadBefore + 1, await (await staff.Client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>());

        // The customer sees their own notification, never the staff one.
        var customerList = (await (await customer.GetAsync("/api/notifications")).ReadJsonAsync()).EnumerateArray().Select(n => n.GetProperty("title").GetString()!).ToList();
        Assert.DoesNotContain(customerList, t => t.StartsWith("New support request"));

        // The notification job e-mails the customer's notification, not the staff bell.
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync();
        }

        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == staff.Email && m.TextBody.Contains(ticket.TicketNumber));
    }
}
