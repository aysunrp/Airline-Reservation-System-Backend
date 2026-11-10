using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Cancelling a Stripe-paid booking over HTTP, the refund it asks Stripe for and the admin refund list. Stripe is
/// <see cref="TestStripeRefundGateway"/>: no request reaches Stripe and no money moves.
/// </summary>
public class RefundEndpointTests : IClassFixture<StripeApiFactory>
{
    private readonly StripeApiFactory _factory;
    private static int _document = 7000000;

    public RefundEndpointTests(StripeApiFactory factory)
    {
        _factory = factory;
        _factory.StripeRefunds.Reset();
    }

    /// <summary>A Business booking (row 1: the fare rules refund it in full ten days ahead) opened on Stripe and confirmed by a signed event.</summary>
    private async Task<BookingDto> StripePaidBookingAsync(HttpClient client, string seat = "1A")
    {
        var seed = await _factory.GetSeedAsync();
        var request = BookingEndpointTests.Request(seed, seat);
        request.Passengers[0].DocumentNumber = $"C{Interlocked.Increment(ref _document)}";
        var created = await client.PostAsJsonAsync("/api/bookings", request, Json);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var booking = await created.ReadAsync<BookingDto>();

        var session = await (await client.PostAsJsonAsync("/api/payments/checkout-session", new { pnr = booking.Pnr, lastName = "Panahli" }, Json)).ReadAsync<CheckoutSessionDto>();
        Payment payment;
        using (var scope = _factory.Services.CreateScope())
        {
            payment = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.AsNoTracking().SingleAsync(p => p.Id == session.PaymentId);
        }

        var payload = StripeTestEvents.CheckoutSession($"evt_refund_{booking.Pnr}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, booking.Id,
            (long)(payment.Amount * 100));
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook") { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
        webhook.Headers.TryAddWithoutValidation("Stripe-Signature", StripeTestEvents.Sign(payload, ApiFactory.TestWebhookSecret));
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().SendAsync(webhook)).StatusCode);
        return booking;
    }

    private async Task<HttpClient> SignedInClientAsync()
        => _factory.ClientWith((await _factory.CreateClient().RegisterAsync()).AccessToken);

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string pnr, string lastName = "Panahli")
        => client.PostAsJsonAsync($"/api/bookings/{pnr}/cancel", new { lastName, reason = "Plans changed" }, Json);

    [Fact]
    public async Task TheOwnerCancels_StripeIsAskedOnce_AndTheRefundIsCompletedFromStripesAnswer()
    {
        var owner = await SignedInClientAsync();
        var booking = await StripePaidBookingAsync(owner);

        // The reference and a passenger's name are not enough for a booking made under an account.
        Assert.Equal(HttpStatusCode.Unauthorized, (await CancelAsync(_factory.CreateClient(), booking.Pnr)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}/cancellation-quote?lastName=Panahli")).StatusCode);

        // Another account sees nothing to cancel.
        var stranger = await SignedInClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await CancelAsync(stranger, booking.Pnr)).StatusCode);
        Assert.Empty(_factory.StripeRefunds.Requests);

        var cancelled = await CancelAsync(owner, booking.Pnr);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var result = await cancelled.ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal((result.Price.Total, RefundStatus.Completed), (result.Cancellation!.RefundAmount, result.Cancellation.RefundStatus));
        Assert.False(result.Cancellation.RefundIsDemo);

        // The amount Stripe was asked for is the one the fare rules worked out on the server, in cents.
        var request = Assert.Single(_factory.StripeRefunds.Requests);
        Assert.Equal(((long)(result.Price.Total * 100), "usd", 1), (request.AmountMinor, request.Currency, request.Attempt));

        // A second cancel is refused and nothing more is sent.
        Assert.Equal(HttpStatusCode.Conflict, (await CancelAsync(owner, booking.Pnr)).StatusCode);
        Assert.Single(_factory.StripeRefunds.Requests);

        var stored = await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}?lastName=Panahli");
        Assert.Equal(PaymentStatus.Refunded, (await stored.ReadAsync<BookingDto>()).PaymentStatus);
    }

    [Fact]
    public async Task AFailedRefund_IsListedForAdmins_AndOnlyASuperAdminCanSendItAgain()
    {
        _factory.StripeRefunds.Answer = _ => new StripeRefundResult(null, "failed", "charge_already_refunded");
        var owner = await SignedInClientAsync();
        var booking = await StripePaidBookingAsync(owner, "1B");

        var cancelled = await (await CancelAsync(owner, booking.Pnr)).ReadAsync<BookingDto>();
        Assert.Equal((BookingStatus.Cancelled, RefundStatus.Failed), (cancelled.Status, cancelled.Cancellation!.RefundStatus));

        var admin = await _factory.CreateAdminClientAsync();
        var failed = await (await admin.GetAsync("/api/admin/refunds?status=Failed")).ReadAsync<List<AdminRefundDto>>();
        var row = Assert.Single(failed, r => r.Pnr == booking.Pnr);
        Assert.Equal((cancelled.Price.Total, "USD", "charge_already_refunded", RefundStatus.Failed), (row.Amount, row.Currency, row.FailureReason, row.Status));
        Assert.Contains("@", row.InitiatedBy);

        // Moving money again is a SuperAdmin decision.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/admin/refunds/{row.Id}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsync($"/api/admin/refunds/{row.Id}/retry", null)).StatusCode);

        _factory.StripeRefunds.Answer = TestStripeRefundGateway.Succeeded;
        var superAdmin = await _factory.CreateSuperAdminClientAsync();
        var retried = await superAdmin.PostAsync($"/api/admin/refunds/{row.Id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        var after = await retried.ReadAsync<AdminRefundDto>();
        Assert.Equal((RefundStatus.Completed, 2), (after.Status, after.Attempts));
        Assert.Equal(new[] { 1, 2 }, _factory.StripeRefunds.Requests.Select(r => r.Attempt));

        // It paid out: another retry is refused, and the amount never changed.
        Assert.Equal(HttpStatusCode.Conflict, (await superAdmin.PostAsync($"/api/admin/refunds/{row.Id}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await superAdmin.PostAsync("/api/admin/refunds/999999/retry", null)).StatusCode);
        Assert.Equal(cancelled.Price.Total, after.Amount);

        // The retry is in the audit log.
        var log = await (await superAdmin.GetAsync("/api/admin/audit-logs?action=RefundRetried")).ReadAsync<List<Service.Helpers.DTOs.Account.AuditLogDto>>();
        Assert.Contains(log, entry => entry.TargetId == row.Id.ToString());
    }

    [Fact]
    public async Task WhenStripeIsOutOfReach_TheBookingIsStillCancelled_AndTheRefundWaits()
    {
        _factory.StripeRefunds.Unavailable = true;
        var owner = await SignedInClientAsync();
        var booking = await StripePaidBookingAsync(owner);

        var response = await CancelAsync(owner, booking.Pnr);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<BookingDto>();
        Assert.Equal((BookingStatus.Cancelled, RefundStatus.Pending), (result.Status, result.Cancellation!.RefundStatus));
        Assert.Empty(_factory.StripeRefunds.Requests);
    }

    [Fact]
    public async Task StripesSignedRefundEvent_FinishesAPendingRefund_OnceAndOnlyWhenTheSignatureIsRight()
    {
        _factory.StripeRefunds.Answer = r => new StripeRefundResult($"re_test_api_{r.RefundId}", "pending", null);
        var owner = await SignedInClientAsync();
        var booking = await StripePaidBookingAsync(owner);
        var cancelled = await (await CancelAsync(owner, booking.Pnr)).ReadAsync<BookingDto>();
        Assert.Equal(RefundStatus.Processing, cancelled.Cancellation!.RefundStatus);

        var admin = await _factory.CreateAdminClientAsync();
        var row = Assert.Single(await (await admin.GetAsync("/api/admin/refunds?take=500")).ReadAsync<List<AdminRefundDto>>(), r => r.Pnr == booking.Pnr);
        Assert.Equal(("pending", RefundStatus.Processing), (row.ProviderStatus, row.Status));

        var payload = StripeTestEvents.Refund("evt_api_refund", "refund.updated", row.ProviderReference!, "succeeded", (long)(row.Amount * 100), "pi_test_0001", row.Id);

        // Wrong or missing signature: refused, nothing changes.
        Assert.Equal(HttpStatusCode.BadRequest, (await SendWebhookAsync(payload, StripeTestEvents.Sign(payload, "whsec_someone_else"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendWebhookAsync(payload, "")).StatusCode);
        Assert.Equal(RefundStatus.Processing, (await GetRefundAsync(admin, row.Id)).Status);

        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(payload, StripeTestEvents.Sign(payload, ApiFactory.TestWebhookSecret))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(payload, StripeTestEvents.Sign(payload, ApiFactory.TestWebhookSecret))).StatusCode);

        var done = await GetRefundAsync(admin, row.Id);
        Assert.Equal((RefundStatus.Completed, "succeeded"), (done.Status, done.ProviderStatus));
        Assert.NotNull(done.ProcessedAt);
        var after = await (await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}?lastName=Panahli")).ReadAsync<BookingDto>();
        Assert.Equal((PaymentStatus.Refunded, RefundStatus.Completed), (after.PaymentStatus, after.Cancellation!.RefundStatus));
    }

    private static async Task<AdminRefundDto> GetRefundAsync(HttpClient admin, int id)
        => Assert.Single(await (await admin.GetAsync("/api/admin/refunds?take=500")).ReadAsync<List<AdminRefundDto>>(), r => r.Id == id);


    private async Task<HttpResponseMessage> SendWebhookAsync(string payload, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook") { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return await _factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task TheRefundList_IsForAdminsOnly()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/refunds")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInClientAsync()).GetAsync("/api/admin/refunds")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await _factory.CreateAdminClientAsync()).GetAsync("/api/admin/refunds?take=5")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await (await _factory.CreateAdminClientAsync()).GetAsync("/api/admin/refunds?status=Nonsense")).StatusCode);
    }
}
