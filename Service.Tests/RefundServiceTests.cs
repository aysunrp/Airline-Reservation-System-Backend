using Domain.Entities;
using Domain.Enums;
using Service.Helpers;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>
/// Cancelling a Stripe-paid booking and the refund that follows. Stripe is <see cref="FakeStripeRefundGateway"/>: nothing is sent
/// and no money moves. "Now" is 1 Nov 2026, the trip leaves on 10 Nov: a Business ticket is refunded in full, an Economy one not at all.
/// </summary>
public class RefundServiceTests
{
    private const int Owner = 1;
    private const string Secret = "whsec_unit_tests_only";
    private readonly BookingTestContext _context = new();

    public RefundServiceTests()
    {
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = Secret;
    }

    /// <summary>A booking paid through Stripe Checkout and confirmed by its webhook.</summary>
    private async Task<(Booking Booking, Payment Payment)> StripePaidAsync(CabinClass cabin = CabinClass.Business, int seatId = 101, int? userId = Owner)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, seatId)));
        request.Legs[0].CabinClass = cabin;
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(userId, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(userId, new CreateCheckoutSessionRequestDto { Pnr = pnr, LastName = "Panahli" });

        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        var payment = booking.Payments.Single();
        var payload = StripeTestEvents.CheckoutSession($"evt_{pnr}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, booking.Id,
            (long)(payment.Amount * 100));
        Assert.Equal(StripeWebhookResult.Handled, await _context.CreateWebhookService().HandleAsync(payload, StripeTestEvents.Sign(payload, Secret)));
        return (booking, payment);
    }

    private static CancelBookingRequestDto Cancel(string pnr, string lastName = "Panahli") => new() { Pnr = pnr, LastName = lastName, Reason = "Change of plans" };

    [Fact]
    public async Task CancellingAStripePaidBooking_AsksStripeForTheAmountTheRulesAllow_AndCompletesTheRefund()
    {
        var (booking, payment) = await StripePaidAsync();

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var refund = Assert.Single(_context.Refunds.All);
        var request = Assert.Single(_context.RefundGateway.Requests);
        Assert.Equal((payment.StripePaymentIntentId, (long)(payment.Amount * 100), "usd", 1), (request.PaymentIntentId, request.AmountMinor, request.Currency, request.Attempt));
        Assert.Equal((RefundStatus.Completed, payment.Amount, "USD", Owner), (refund.Status, refund.Amount, refund.Currency, refund.InitiatedByUserId));
        Assert.Equal($"re_test_{refund.Id}_1", refund.ProviderReference);
        Assert.Equal("succeeded", refund.ProviderStatus);
        Assert.NotNull(refund.ProcessedAt);
        Assert.Equal("Change of plans", refund.Reason);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal((BookingStatus.Cancelled, RefundStatus.Completed), (result.Status, result.Cancellation!.RefundStatus));
        Assert.False(result.Cancellation.RefundIsDemo);
        Assert.Contains(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task APendingStripeRefund_IsProcessing_NotCompleted_UntilStripeSaysSucceeded()
    {
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, payment) = await StripePaidAsync();

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var refund = Assert.Single(_context.Refunds.All);
        Assert.Equal((RefundStatus.Processing, "pending", (DateTime?)null), (refund.Status, refund.ProviderStatus, refund.ProcessedAt));
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(RefundStatus.Processing, result.Cancellation!.RefundStatus);
        Assert.DoesNotContain(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task ARefusedRefund_IsFailed_TheBookingStaysCancelled_AndThePaymentStaysPaid()
    {
        _context.RefundGateway.Answer = _ => new StripeRefundResult(null, "failed", "charge_already_refunded");
        var (booking, payment) = await StripePaidAsync();

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var refund = Assert.Single(_context.Refunds.All);
        Assert.Equal((RefundStatus.Failed, "charge_already_refunded", (string?)null), (refund.Status, refund.FailureReason, refund.ProviderReference));
        Assert.Equal((BookingStatus.Cancelled, PaymentStatus.Paid), (result.Status, payment.Status));
        Assert.DoesNotContain(await _context.Notifications.GetAllAsync(), n => n.Type == NotificationType.RefundCompleted);
    }

    [Fact]
    public async Task WhenStripeCannotBeReached_TheCancellationStands_TheRefundStaysQueued_AndTheJobSendsItLater()
    {
        _context.RefundGateway.Unavailable = true;
        var (booking, _) = await StripePaidAsync();

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var refund = Assert.Single(_context.Refunds.All);
        Assert.Equal((BookingStatus.Cancelled, RefundStatus.Pending, (string?)null), (result.Status, refund.Status, refund.ProviderReference));
        refund.CreatedAt = TestData.Now;

        // Too fresh for the job: the cancel request itself may still be sending it.
        Assert.Equal(0, await _context.CreateRefundService().SubmitDueAsync());

        _context.RefundGateway.Unavailable = false;
        refund.CreatedAt = TestData.Now.AddMinutes(-5);
        Assert.Equal(1, await _context.CreateRefundService().SubmitDueAsync());
        Assert.Equal(RefundStatus.Completed, refund.Status);
    }

    [Fact]
    public async Task SendingTheSameRefundTwice_AsksStripeOnceAndNeverPaysBackTwice()
    {
        var (booking, payment) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);
        var refund = Assert.Single(_context.Refunds.All);

        var service = _context.CreateRefundService();
        await service.SubmitAsync(refund.Id);
        await service.SubmitAsync(refund.Id);

        Assert.Single(_context.RefundGateway.Requests);
        Assert.Equal(1, _context.RefundGateway.Calls);
        Assert.Equal(payment.Amount, _context.Refunds.All.Sum(r => r.Amount));

        // A second cancel of the same booking is refused before any refund is made.
        await Assert.ThrowsAsync<ConflictException>(() => _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner));
        Assert.Single(_context.Refunds.All);
    }

    [Fact]
    public async Task ARefundThatWouldPassWhatWasPaid_IsNeverSent()
    {
        // Stripe keeps the first refund open (pending), so the payment is still Paid when the second one is asked for.
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, payment) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);
        var first = Assert.Single(_context.Refunds.All);

        // A second request on the same payment, as a bug or a race could make one.
        var extra = new Refund { Id = 900, CancellationId = first.CancellationId, PaymentId = payment.Id, Amount = 10m, Currency = "USD", Cancellation = first.Cancellation, Payment = payment, Attempts = 1 };
        first.Cancellation.Refunds.Add(extra);

        await _context.CreateRefundService().SubmitAsync(extra.Id);

        Assert.Equal((RefundStatus.Failed, "The refunds would be more than the amount paid."), (extra.Status, extra.FailureReason));
        Assert.Single(_context.RefundGateway.Requests);
    }

    [Fact]
    public async Task TheEconomyBookingInsideTheNoRefundWindow_GetsNoStripeRefund()
    {
        var (booking, payment) = await StripePaidAsync(CabinClass.Economy, 103);

        var result = await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var refund = Assert.Single(_context.Refunds.All);
        Assert.Equal((RefundStatus.NotEligible, 0m), (refund.Status, refund.Amount));
        Assert.Empty(_context.RefundGateway.Requests);
        Assert.Equal((BookingStatus.Cancelled, PaymentStatus.Paid), (result.Status, payment.Status));
    }

    [Fact]
    public async Task ARetry_IsANewStripeRefund_ByAttempt_AndOnlyForARefundThatFailed()
    {
        _context.RefundGateway.Answer = _ => new StripeRefundResult(null, "failed", "insufficient_funds");
        var (booking, payment) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);
        var refund = Assert.Single(_context.Refunds.All);

        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}_{r.Attempt}", "succeeded", null);
        var dto = await _context.CreateRefundService().RetryAsync(refund.Id, new AuditActor(9, "SuperAdmin"));

        Assert.Equal((RefundStatus.Completed, 2, PaymentStatus.Refunded), (dto.Status, dto.Attempts, payment.Status));
        Assert.Equal(new[] { 1, 2 }, _context.RefundGateway.Requests.Select(r => r.Attempt));
        var audit = Assert.Single(_context.AuditEntries.Entries);
        Assert.Equal((AuditActions.RefundRetried, "Refund", 9), (audit.Action, audit.TargetType, audit.ActorUserId));

        // It paid out: sending it again is refused.
        await Assert.ThrowsAsync<ConflictException>(() => _context.CreateRefundService().RetryAsync(refund.Id, new AuditActor(9, "SuperAdmin")));
        Assert.Equal(2, _context.RefundGateway.Requests.Count);
    }

    [Fact]
    public async Task StripeStatuses_AreMapped_AndALateMessageNeverUndoesAResult()
    {
        Assert.Equal(RefundStatus.Completed, StripeRefundStatusMapper.ToRefundStatus("succeeded"));
        Assert.Equal(RefundStatus.Processing, StripeRefundStatusMapper.ToRefundStatus("pending"));
        Assert.Equal(RefundStatus.Processing, StripeRefundStatusMapper.ToRefundStatus("requires_action"));
        Assert.Equal(RefundStatus.Failed, StripeRefundStatusMapper.ToRefundStatus("failed"));
        Assert.Equal(RefundStatus.Cancelled, StripeRefundStatusMapper.ToRefundStatus("canceled"));
        Assert.Equal(RefundStatus.Processing, StripeRefundStatusMapper.ToRefundStatus("something_new"));

        var (booking, _) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);
        var refund = Assert.Single(_context.Refunds.All);

        // A late "pending" never undoes a result.
        var late = await _context.CreateRefundService().ApplyProviderStateAsync(refund, refund.ProviderReference, "pending", null);
        Assert.StartsWith("Ignored", late);
        Assert.Equal(RefundStatus.Completed, refund.Status);

        var other = await _context.CreateRefundService().ApplyProviderStateAsync(refund, "re_someone_else", "succeeded", null);
        Assert.StartsWith("Rejected", other);
    }

    [Fact]
    public async Task OnlyTheAccountThatMadeTheBooking_CanCancelIt_AGuestBookingNeedsTheLastName()
    {
        var (owned, _) = await StripePaidAsync();

        // Neither the last name alone nor another account is enough for a booking made under an account.
        await Assert.ThrowsAsync<UnauthorizedException>(() => _context.CreateCancellationService().CancelAsync(Cancel(owned.Pnr)));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _context.CreateCancellationService().GetQuoteAsync(owned.Pnr, "Panahli"));
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCancellationService().CancelAsync(Cancel(owned.Pnr), 2));
        Assert.Empty(_context.Refunds.All);
        Assert.Equal(BookingStatus.Confirmed, owned.Status);

        var (guest, _) = await StripePaidAsync(CabinClass.Business, 102, userId: null);
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCancellationService().CancelAsync(Cancel(guest.Pnr, "Aliyev")));
        var result = await _context.CreateCancellationService().CancelAsync(Cancel(guest.Pnr));
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Null(Assert.Single(_context.Refunds.All).InitiatedByUserId);
    }

    [Fact]
    public async Task AnAdminCancellation_RecordsWhoStartedIt_AndFollowsTheSameRules()
    {
        var (booking, _) = await StripePaidAsync();

        await _context.CreateCancellationService().CancelByAdminAsync(booking.Pnr, "Aircraft change", adminUserId: 77);

        var refund = Assert.Single(_context.Refunds.All);
        Assert.Equal((77, "Aircraft change", RefundStatus.Completed), (refund.InitiatedByUserId, refund.Reason, refund.Status));
    }

    [Fact]
    public async Task ThePretendRefundJob_NeverTouchesAStripeRefund()
    {
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, _) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);
        var refund = Assert.Single(_context.Refunds.All);

        _context.Clock = new FixedTimeProvider(TestData.Now.AddDays(1));
        var processing = new Service.Services.RefundProcessingService(_context.Bookings, new Service.Services.NotificationService(_context.Notifications),
            Microsoft.Extensions.Options.Options.Create(new Service.Helpers.Settings.BookingLifecycleSettings()), _context.Clock);
        var result = await processing.ProcessDueAsync();

        Assert.Equal((0, 0), (result.Processing, result.Completed));
        Assert.Equal(RefundStatus.Processing, refund.Status);
        Assert.Null(refund.ProviderReference is { } r && r.StartsWith("MOCK") ? r : null);
    }

    [Fact]
    public async Task TheAdminList_ShowsEveryRefundWithItsBookingAndWhoStartedIt()
    {
        _context.RefundGateway.Answer = _ => new StripeRefundResult(null, "failed", "charge_already_refunded");
        var (booking, _) = await StripePaidAsync();
        await _context.CreateCancellationService().CancelAsync(Cancel(booking.Pnr), Owner);

        var all = await _context.CreateRefundService().GetAllAsync(null, 50);
        var failed = await _context.CreateRefundService().GetAllAsync(RefundStatus.Failed, 50);
        var completed = await _context.CreateRefundService().GetAllAsync(RefundStatus.Completed, 50);

        var row = Assert.Single(all);
        Assert.Equal((booking.Pnr, RefundStatus.Failed, "charge_already_refunded", "User 1"), (row.Pnr, row.Status, row.FailureReason, row.InitiatedBy));
        Assert.Single(failed);
        Assert.Empty(completed);
    }
}
