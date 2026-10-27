using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mail;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>
/// The e-mails of a cancellation and its refund. Stripe is <see cref="FakeStripeRefundGateway"/> and the mail server
/// <see cref="FakeEmailSender"/>: no money moves and nothing is sent. "Now" is 1 Nov 2026, the trip leaves on 10 Nov: Business is
/// refunded in full 7 days or more before departure, and with a 15% fee from 1 to 6 days before.
/// </summary>
public class RefundEmailTests
{
    private const int Owner = 1;
    private const string Secret = "whsec_unit_tests_only";
    private readonly BookingTestContext _context = new();
    private readonly User _account = new()
    {
        Id = Owner, Email = "aysun@example.com", FirstName = "Aysun", LastName = "Panahli", PasswordHash = "x", EmailVerifiedAt = TestData.Now.AddDays(-3)
    };

    public RefundEmailTests()
    {
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = Secret;
        _context.Users.AddAsync(_account).GetAwaiter().GetResult();
    }

    private async Task<(Booking Booking, Payment Payment)> StripePaidAsync()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 101)));
        request.Legs[0].CabinClass = CabinClass.Business;
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(Owner, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(Owner, new CreateCheckoutSessionRequestDto { Pnr = pnr, LastName = "Panahli" });
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        var payment = booking.Payments.Single();
        var paid = StripeTestEvents.CheckoutSession($"evt_pay_{pnr}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, booking.Id, (long)(payment.Amount * 100));
        Assert.Equal(StripeWebhookResult.Handled, await SendAsync(paid));
        await PaymentNewsSentAsync();
        return (booking, payment);
    }

    /// <summary>The "booking confirmed" and "payment received" news went out when the booking was paid; only what follows is counted here.</summary>
    private async Task PaymentNewsSentAsync()
    {
        foreach (var notification in await _context.Notifications.GetAllAsync())
        {
            notification.EmailSentAt ??= TestData.Now;
        }
    }

    private Task<StripeWebhookResult> SendAsync(string payload)
        => _context.CreateWebhookService().HandleAsync(payload, StripeTestEvents.Sign(payload, Secret));

    private Task<BookingDto> CancelAsync(Booking booking, string? language = "en")
        => _context.CreateCancellationService().CancelAsync(new CancelBookingRequestDto { Pnr = booking.Pnr, LastName = "Panahli", Reason = "Change of plans", Language = language }, Owner);

    private static string RefundEvent(Refund refund, Payment payment, string eventId, string status, string? failure = null)
        => StripeTestEvents.Refund(eventId, "refund.updated", refund.ProviderReference ?? $"re_test_{refund.Id}", status, (long)(refund.Amount * 100),
            payment.StripePaymentIntentId!, refund.Id, "usd", failure);

    private NotificationEmailService CreateMailer() => new(
        _context.Notifications,
        _context.Bookings,
        _context.EmailSender,
        Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test/" }),
        _context.Clock,
        new ListLogger<NotificationEmailService>());

    /// <summary>Sends what is due, as the background job does; the fake repository does not join the account and booking itself.</summary>
    private async Task<int> MailAsync()
    {
        foreach (var notification in await _context.Notifications.GetAllAsync())
        {
            notification.User = _account;
            // The fake repository stamps the real clock; the e-mail job only takes notifications of the last day by the test clock.
            notification.CreatedAt = _context.Clock.GetUtcNow().UtcDateTime;
            notification.Booking = notification.BookingId is int id ? await _context.Bookings.GetByIdAsync(id) : null;
        }

        return await CreateMailer().SendDueAsync();
    }

    private async Task<IReadOnlyList<Notification>> NotificationsAsync(NotificationType type)
        => (await _context.Notifications.GetAllAsync()).Where(n => n.Type == type).ToList();

    [Fact]
    public async Task Cancelling_SendsTheCancellationEmail_WithTheSavedAmounts_InThePageLanguage()
    {
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, payment) = await StripePaidAsync();

        await CancelAsync(booking, "az");
        await MailAsync();

        var cancelled = _context.EmailSender.Sent.Single(m => m.Subject.Contains("ləğv edildi"));
        Assert.Equal("aysun@example.com", cancelled.To);
        foreach (var part in new[] { booking.Pnr, "AV 205", "GYD → LHR", "Change of plans", $"{payment.Amount:0.00} USD".Replace(',', '.'), "ödəniş etdiyiniz karta" })
        {
            Assert.Contains(part, cancelled.TextBody);
        }
        Assert.Contains("#0b1a2c", cancelled.HtmlBody);
        Assert.Contains("max-width:560px", cancelled.HtmlBody);
    }

    [Fact]
    public async Task APendingRefund_SaysStarted_NeverConfirmed()
    {
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, _) = await StripePaidAsync();

        await CancelAsync(booking);
        await MailAsync();

        var pending = _context.EmailSender.Sent.Single(m => m.Subject.Contains("has started"));
        Assert.Contains("still processing", pending.TextBody);
        Assert.Contains("5 to 10 business days", pending.TextBody);
        Assert.Contains("10 business days, please contact your bank", pending.TextBody);
        Assert.DoesNotContain("confirmed", pending.Subject);
        Assert.DoesNotContain(_context.EmailSender.Sent, m => m.Subject.Contains("is confirmed"));
    }

    [Fact]
    public async Task ASucceededRefund_SendsTheFinalEmail_Once_EvenWhenStripeRepeatsTheEvent()
    {
        _context.RefundGateway.Answer = r => new StripeRefundResult($"re_test_{r.RefundId}", "pending", null);
        var (booking, payment) = await StripePaidAsync();
        await CancelAsync(booking);
        var refund = _context.Refunds.All.Single();

        Assert.Equal(StripeWebhookResult.Handled, await SendAsync(RefundEvent(refund, payment, "evt_ok_1", "succeeded")));
        await SendAsync(RefundEvent(refund, payment, "evt_ok_1", "succeeded"));
        await SendAsync(RefundEvent(refund, payment, "evt_ok_2", "succeeded"));
        await MailAsync();
        await MailAsync();

        Assert.Single(await NotificationsAsync(NotificationType.RefundCompleted));
        var confirmed = Assert.Single(_context.EmailSender.Sent, m => m.Subject.Contains("is confirmed"));
        Assert.Contains("Stripe has confirmed the refund", confirmed.TextBody);
        Assert.Contains("estimate, not a guarantee", confirmed.TextBody);
        Assert.Equal(3, _context.EmailSender.Sent.Count); // cancelled, started, confirmed
    }

    [Fact]
    public async Task AFailedRefund_SaysSo_AndNeverThatTheMoneyCameBack()
    {
        _context.RefundGateway.Answer = _ => new StripeRefundResult(null, "failed", "charge_already_refunded");
        var (booking, _) = await StripePaidAsync();

        await CancelAsync(booking);
        await MailAsync();

        Assert.Equal(RefundStatus.Failed, _context.Refunds.All.Single().Status);
        var failed = Assert.Single(_context.EmailSender.Sent, m => m.Subject.Contains("could not be completed"));
        Assert.Contains("still owed to you", failed.TextBody);
        Assert.Contains("contact AEROVA support with booking reference " + booking.Pnr, failed.TextBody);
        Assert.Contains("Not completed", failed.TextBody);
        foreach (var mail in _context.EmailSender.Sent)
        {
            Assert.DoesNotContain("Stripe has confirmed", mail.TextBody);
            Assert.DoesNotContain("is confirmed", mail.Subject);
        }
    }

    [Fact]
    public async Task APartialRefund_ShowsThePaidAmount_TheFee_TheRefund_AndWhatIsKept()
    {
        var (booking, payment) = await StripePaidAsync();
        // Five days before departure Business keeps a 15% fee.
        _context.Clock = new FixedTimeProvider(TestData.Now.AddDays(4));

        var result = await CancelAsync(booking);
        await MailAsync();

        var fee = result.Cancellation!.FeeAmount;
        var refund = result.Cancellation.RefundAmount;
        Assert.True(fee > 0 && refund > 0 && fee + refund == payment.Amount);
        var cancelled = _context.EmailSender.Sent.Single(m => m.Subject.Contains("is cancelled"));
        string Money(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " USD";
        Assert.Contains("Amount paid: " + Money(payment.Amount), cancelled.TextBody);
        Assert.Contains("Cancellation fee: " + Money(fee), cancelled.TextBody);
        Assert.Contains("Refund: " + Money(refund), cancelled.TextBody);
        Assert.Contains("Not refunded: " + Money(fee), cancelled.TextBody);
    }

    [Fact]
    public async Task WhenNoRefundIsDue_TheEmailSaysSo()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        var pnr = (await _context.CreateBookingService().CreateBookingAsync(Owner, request)).Pnr;
        await _context.CreateCheckoutService().CreateSessionAsync(Owner, new CreateCheckoutSessionRequestDto { Pnr = pnr, LastName = "Panahli" });
        var booking = (await _context.Bookings.GetByPnrAsync(pnr))!;
        var payment = booking.Payments.Single();
        await SendAsync(StripeTestEvents.CheckoutSession($"evt_pay_{pnr}", "checkout.session.completed", payment.StripeCheckoutSessionId!, payment.Id, booking.Id, (long)(payment.Amount * 100)));
        await PaymentNewsSentAsync();

        await CancelAsync(booking);
        await MailAsync();

        var mail = Assert.Single(_context.EmailSender.Sent);
        Assert.Contains("no refund is due", mail.TextBody);
        Assert.DoesNotContain("Refunded to", mail.TextBody);
    }

    [Fact]
    public async Task AnSmtpFailure_LeavesTheRefundAsItIs_AndTheEmailIsTriedAgainLater()
    {
        var (booking, _) = await StripePaidAsync();
        await CancelAsync(booking);
        var refund = _context.Refunds.All.Single();
        Assert.Equal(RefundStatus.Completed, refund.Status);

        _context.EmailSender.Fail = true;
        Assert.Equal(0, await MailAsync());

        Assert.Equal(RefundStatus.Completed, refund.Status);
        Assert.All((await _context.Notifications.GetAllAsync()).Where(n => n.EmailKey != null), n => Assert.Equal((1, (DateTime?)null), (n.EmailAttempts, n.EmailSentAt)));

        _context.EmailSender.Fail = false;
        Assert.Equal(2, await MailAsync()); // cancelled and confirmed, now delivered
        Assert.Equal(RefundStatus.Completed, refund.Status);
    }

    [Fact]
    public async Task SomebodyElse_CannotCancelTheBooking_SoNoEmailGoesOut()
    {
        var (booking, _) = await StripePaidAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCancellationService().CancelAsync(
            new CancelBookingRequestDto { Pnr = booking.Pnr, LastName = "Panahli" }, 99));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _context.CreateCancellationService().CancelAsync(
            new CancelBookingRequestDto { Pnr = booking.Pnr, LastName = "Panahli" }, null));

        Assert.Empty(_context.Refunds.All);
        Assert.Equal(0, await MailAsync());
    }

    [Fact]
    public async Task CancellingTwice_GivesNoSecondRefund_AndNoSecondEmail()
    {
        var (booking, _) = await StripePaidAsync();
        await CancelAsync(booking);

        await Assert.ThrowsAsync<ConflictException>(() => CancelAsync(booking));
        await MailAsync();

        Assert.Single(_context.Refunds.All);
        Assert.Single(await NotificationsAsync(NotificationType.BookingCancelled));
        Assert.Single(_context.EmailSender.Sent, m => m.Subject.Contains("is cancelled"));
    }

    [Theory]
    [InlineData(RefundEmailKind.Cancelled, "en", "is cancelled")]
    [InlineData(RefundEmailKind.RefundPending, "ru", "начат")]
    [InlineData(RefundEmailKind.RefundCompleted, "az", "təsdiqləndi")]
    [InlineData(RefundEmailKind.RefundFailed, "xx", "could not be completed")]
    public void TheTemplate_IsWrittenInTheLanguage_AndEncodesWhatItShows(RefundEmailKind kind, string language, string inSubject)
    {
        var details = new RefundEmailDetails("ABC123", new[] { new RefundEmailFlight("AV 205", "GYD", "LHR", TestData.Now) }, TestData.Now,
            "<script>x</script>", 100m, 15m, 85m, "usd", true, true, "https://aerova.test/my-trips.html");

        var (subject, _, html) = RefundEmailTemplate.Build(language, kind, "Aysun", details);

        Assert.Contains(inSubject, subject);
        Assert.Contains("ABC123", subject);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("85.00 USD", html);
    }
}
