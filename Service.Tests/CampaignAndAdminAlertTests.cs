using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

internal class FakeAdminAlertRepository : FakeRepository<AdminAlert>, IAdminAlertRepository
{
    public IReadOnlyList<AdminAlert> All => Items;

    public virtual Task<bool> KeyExistsAsync(string eventKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(a => a.EventKey == eventKey));

    public virtual Task<IReadOnlyList<AdminAlert>> GetDueAsync(int maxAttempts, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AdminAlert>>(Items.Where(a => a.SentAt == null && a.Attempts < maxAttempts).OrderBy(a => a.Id).Take(take).ToList());
}

internal sealed class FakePromoRepository : FakeRepository<PromoCode>
{
    public FakePromoRepository(params PromoCode[] items) : base(items)
    {
    }

    public IReadOnlyList<PromoCode> All => Items;
}

internal class FakeEmailCampaignRepository : FakeRepository<EmailCampaign>, IEmailCampaignRepository
{
    public IReadOnlyList<EmailCampaign> All => Items;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var id = 1;
        foreach (var campaign in Items.Where(c => c.Id == 0))
        {
            campaign.Id = Items.Max(c => c.Id) + id++;
        }

        return Task.FromResult(1);
    }

    public virtual Task<EmailCampaign?> GetByPromoCodeIdAsync(int promoCodeId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.PromoCodeId == promoCodeId));

    public Task<IReadOnlyList<EmailCampaign>> GetOpenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailCampaign>>(Items.Where(c => c.Status is CampaignStatus.Queued or CampaignStatus.Sending).ToList());

    public virtual Task<IReadOnlyList<EmailCampaign>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailCampaign>>(Items.OrderByDescending(c => c.Id).Take(take).ToList());

    public Task<IReadOnlyDictionary<CampaignRecipientStatus, int>> GetCountsAsync(int campaignId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<CampaignRecipientStatus, int>>(
            Items.Single(c => c.Id == campaignId).Recipients.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<IReadOnlyList<EmailCampaignRecipient>> GetPendingBatchAsync(int campaignId, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailCampaignRecipient>>(
            Items.Single(c => c.Id == campaignId).Recipients.Where(r => r.Status == CampaignRecipientStatus.Pending).Take(take).ToList());
}

/// <summary>
/// E-mails to the admin about business events, and promotion e-mail campaigns. The mail server is always a stand-in: no real e-mail is
/// sent, and nothing here proves delivery through Gmail.
/// </summary>
public class CampaignAndAdminAlertTests
{
    private readonly FakeEmailSender _sender = new();
    private readonly MovableClock _clock = new();
    private readonly FakeAdminAlertRepository _alerts = new();

    private AdminAlertService CreateAlerts(string recipient = "admin@aerova.test", bool enabled = true, int maxAttempts = 3, string sender = "sender@aerova.test")
        => new(_alerts, _sender, Options.Create(new AdminNotificationSettings { Enabled = enabled, RecipientEmail = recipient, MaxAttempts = maxAttempts }),
            Options.Create(new SmtpSettings { SenderEmail = sender }), Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" }), _clock,
            NullLogger<AdminAlertService>.Instance);

    // ---------------- admin notifications ----------------

    [Fact]
    public async Task ConfirmedBookingAndPayment_AlertTheAdmin_OncePerBooking()
    {
        var notifications = new NotificationService(new FakeNotificationRepository(), CreateAlerts());

        await notifications.QueueAsync(7, 11, NotificationType.BookingConfirmed, "Booking Confirmed", "Your booking AV1234 is confirmed.");
        await notifications.QueueAsync(7, 11, NotificationType.PaymentSuccessful, "Payment Successful", "We received your payment of 930.60 for booking AV1234.");

        Assert.Equal(new[] { "Booking confirmed", "Payment confirmed" }, _alerts.All.Select(a => a.Subject));
        Assert.Contains("AV1234", _alerts.All[1].Body);
        Assert.Contains("Booking id: 11", _alerts.All[1].Body);
    }

    [Fact]
    public async Task EachRefundStage_AlertsTheAdmin_AndARetriedWebhookDoesNotAlertTwice()
    {
        var notifications = new NotificationService(new FakeNotificationRepository(), CreateAlerts());

        await notifications.QueueOnceAsync("booking:11:cancelled", 7, 11, NotificationType.BookingCancelled, "Booking Cancelled", "Your booking AV1234 has been cancelled. A refund of 100.00 USD has been requested.");
        await notifications.QueueOnceAsync("refund:5:1:processing", 7, 11, NotificationType.RefundPending, "Refund Started", "The refund for booking AV1234 is being processed.");
        await notifications.QueueOnceAsync("refund:5:1:completed", 7, 11, NotificationType.RefundCompleted, "Refund Completed", "The refund for booking AV1234 has been confirmed.");
        await notifications.QueueOnceAsync("refund:6:1:failed", 7, 11, NotificationType.RefundFailed, "Refund Not Completed", "The refund for booking AV1234 could not be completed.");

        // The same Stripe events delivered again.
        var repeated = new NotificationService(new FakeNotificationRepository(), CreateAlerts());
        await repeated.QueueOnceAsync("refund:5:1:completed", 7, 11, NotificationType.RefundCompleted, "Refund Completed", "again");

        Assert.Equal(new[] { "Booking cancelled", "Refund processing", "Refund succeeded", "Refund FAILED" }, _alerts.All.Select(a => a.Subject));
    }

    [Fact]
    public async Task ARepeatedEvent_OfTheSameNotification_AddsNoSecondAlert()
    {
        var repository = new FakeNotificationRepository();
        var notifications = new NotificationService(repository, CreateAlerts());

        Assert.True(await notifications.QueueOnceAsync("refund:5:1:completed", 7, 11, NotificationType.RefundCompleted, "Refund Completed", "done"));
        Assert.False(await notifications.QueueOnceAsync("refund:5:1:completed", 7, 11, NotificationType.RefundCompleted, "Refund Completed", "done"));

        Assert.Single(_alerts.All);
    }

    [Fact]
    public async Task NotificationsThatAreNotBusinessEvents_DoNotAlertTheAdmin()
    {
        var notifications = new NotificationService(new FakeNotificationRepository(), CreateAlerts());

        await notifications.QueueAsync(7, 11, NotificationType.CheckInReminder, "Check-in", "Check-in opens soon.");
        await notifications.QueueAsync(7, 11, NotificationType.FlightUpdate, "Flight update", "Your flight changed.");

        Assert.Empty(_alerts.All);
    }

    [Fact]
    public async Task WithoutAdminNotificationsSwitchedOn_OrWithoutAnyAddress_NothingIsQueued()
    {
        await CreateAlerts(enabled: false).QueueAsync("k1", "Subject", "Body");
        await CreateAlerts(recipient: "", sender: "").QueueAsync("k2", "Subject", "Body");

        Assert.Empty(_alerts.All);
    }

    private sealed class BrokenAlertRepository : FakeAdminAlertRepository
    {
        public override Task<bool> KeyExistsAsync(string eventKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Invalid object name 'AdminAlerts'.");

        public override Task<IReadOnlyList<AdminAlert>> GetDueAsync(int maxAttempts, int take, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Invalid object name 'AdminAlerts'.");
    }

    [Fact]
    public async Task WhenTheAlertTableIsMissing_TheEventGoesOn_AndNothingIsAddedToItsUnitOfWork()
    {
        var broken = new BrokenAlertRepository();
        var service = new AdminAlertService(broken, _sender, Options.Create(new AdminNotificationSettings { RecipientEmail = "admin@aerova.test" }),
            Options.Create(new SmtpSettings()), Options.Create(new AuthSettings()), _clock, NullLogger<AdminAlertService>.Instance);
        var notifications = new NotificationService(new FakeNotificationRepository(), service);

        await notifications.QueueAsync(7, 11, NotificationType.PaymentSuccessful, "Payment Successful", "We received your payment.");
        await service.QueueAndSaveAsync("k", "Subject", "Body");

        Assert.Empty(broken.All);
        Assert.Equal(0, await service.SendDueAsync());
    }

    [Fact]
    public async Task TheAdminAddress_FallsBackToTheMailAccountAddress()
    {
        var service = CreateAlerts(recipient: "", sender: "owner@gmail.test");
        await service.QueueAsync("k1", "Booking confirmed", "Body");

        await service.SendDueAsync();

        Assert.Equal("owner@gmail.test", Assert.Single(_sender.Sent).To);
    }

    [Fact]
    public async Task AnAlert_IsSentToTheConfiguredAdmin_OnlyOnce_WithTheDashboardLink()
    {
        var service = CreateAlerts();
        await service.QueueAsync("k1", "Payment confirmed", "We received 10.00 for AV1234.");

        Assert.Equal(1, await service.SendDueAsync());
        Assert.Equal(0, await service.SendDueAsync());

        var message = Assert.Single(_sender.Sent);
        Assert.Equal("admin@aerova.test", message.To);
        Assert.Equal("[AEROVA] Payment confirmed", message.Subject);
        Assert.Contains("https://aerova.test/admin.html", message.TextBody);
        Assert.NotNull(_alerts.All[0].SentAt);
    }

    [Fact]
    public async Task WhenTheMailServerFails_TheAlertIsKept_RetriedAndGivenUpAfterTheAllowedAttempts()
    {
        var service = CreateAlerts(maxAttempts: 2);
        await service.QueueAsync("k1", "Refund FAILED", "Body");
        _sender.Fail = true;

        await service.SendDueAsync();
        await service.SendDueAsync();
        await service.SendDueAsync();

        Assert.Equal(2, _alerts.All[0].Attempts);
        Assert.Null(_alerts.All[0].SentAt);
        Assert.Single(_alerts.All);

        // A recovered mail server sends one that has attempts left.
        var other = CreateAlerts(maxAttempts: 5);
        await other.QueueAsync("k2", "Booking confirmed", "Body");
        _sender.Fail = true;
        await other.SendDueAsync();
        _sender.Fail = false;
        Assert.True(await other.SendDueAsync() >= 1);
        Assert.NotNull(_alerts.All.Single(a => a.EventKey == "k2").SentAt);
    }

    [Fact]
    public async Task WithoutMailSettings_NothingIsSent_AndNoAttemptIsUsed()
    {
        var service = CreateAlerts();
        await service.QueueAsync("k1", "Booking confirmed", "Body");
        _sender.IsConfigured = false;

        Assert.Equal(0, await service.SendDueAsync());
        Assert.Equal(0, _alerts.All[0].Attempts);
    }

    [Fact]
    public async Task ANewConfirmedSubscriber_AlertsTheAdmin()
    {
        var subscribers = new FakeNewsletterSubscriberRepository();
        var newsletter = new NewsletterService(subscribers, _sender, new NewsletterSubscribeRequestValidator(), new NewsletterTokenRequestValidator(),
            Options.Create(new NewsletterSettings()), Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" }),
            Options.Create(new JwtSettings { Secret = "campaign-tests-secret-campaign-tests-secret" }), _clock, NullLogger<NewsletterService>.Instance, CreateAlerts());

        await newsletter.SubscribeAsync(new NewsletterSubscribeRequestDto { Email = "ayla@example.com", Language = "en" });
        Assert.Empty(_alerts.All);

        var start = _sender.Sent[0].TextBody.IndexOf("newsletter-confirm.html?token=", StringComparison.Ordinal) + "newsletter-confirm.html?token=".Length;
        var token = Uri.UnescapeDataString(_sender.Sent[0].TextBody[start.._sender.Sent[0].TextBody.IndexOf('\n', start)].Trim());
        await newsletter.ConfirmAsync(new NewsletterTokenRequestDto { Token = token });

        var alert = Assert.Single(_alerts.All);
        Assert.Equal("New newsletter subscriber", alert.Subject);
        Assert.Contains("ayla@example.com", alert.Body);
    }

    // ---------------- promotion campaigns ----------------

    private sealed class Campaigns
    {
        public required CampaignService Service { get; init; }
        public required FakeEmailCampaignRepository Repository { get; init; }
        public required FakeNewsletterSubscriberRepository Subscribers { get; init; }
        public required FakePromoRepository Promos { get; init; }
    }

    private Campaigns CreateCampaigns(NewsletterSettings? settings = null, params NewsletterSubscriber[] subscribers)
    {
        var promo = new PromoCode { Id = 1, Code = "WINTER20", DiscountPercent = 20, ValidTo = _clock.GetUtcNow().UtcDateTime.AddDays(30), MaxUses = 100, IsActive = true };
        var subscriberRepository = new FakeNewsletterSubscriberRepository(subscribers);
        var promos = new FakePromoRepository(promo);
        var repository = new FakeEmailCampaignRepository();
        var auth = Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" });
        var newsletterSettings = Options.Create(settings ?? new NewsletterSettings());
        var newsletter = new NewsletterService(subscriberRepository, _sender, new NewsletterSubscribeRequestValidator(), new NewsletterTokenRequestValidator(),
            newsletterSettings, auth, Options.Create(new JwtSettings { Secret = "campaign-tests-secret-campaign-tests-secret" }), _clock, NullLogger<NewsletterService>.Instance);
        var service = new CampaignService(promos, repository, subscriberRepository, newsletter, CreateAlerts(), _sender, newsletterSettings, auth, _clock,
            NullLogger<CampaignService>.Instance);
        return new Campaigns { Service = service, Repository = repository, Subscribers = subscriberRepository, Promos = promos };
    }

    private static NewsletterSubscriber Sub(int id, string email, NewsletterStatus status = NewsletterStatus.Active, string language = "en")
        => new() { Id = id, Email = email, Status = status, Language = language, UnsubscribeKey = Guid.NewGuid() };

    private static CreateCampaignRequestDto Confirm(string hash) => new() { PreviewHash = hash, Confirm = true };

    private async Task<CampaignDto> QueueAsync(Campaigns c)
    {
        var preview = await c.Service.PreviewAsync(1, "en");
        return await c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99);
    }

    [Fact]
    public async Task ThePreview_ShowsTheRealPromotion_AndCountsOnlyConfirmedSubscribers_AndSendsNothing()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"), Sub(2, "pending@example.com", NewsletterStatus.Pending), Sub(3, "left@example.com", NewsletterStatus.Unsubscribed));

        var preview = await c.Service.PreviewAsync(1, "az");

        Assert.Equal("WINTER20", preview.Code);
        Assert.Equal(20, preview.DiscountPercent);
        Assert.Equal(1, preview.RecipientCount);
        Assert.True(preview.CanSend);
        Assert.Contains("WINTER20", preview.Text);
        Assert.Contains("20%", preview.Subject);
        Assert.Contains("Abunəlikdən çıx", preview.Text);
        Assert.Empty(_sender.Sent);
        Assert.Empty(c.Repository.All);
    }

    [Theory]
    [InlineData("inactive", "promo_inactive")]
    [InlineData("expired", "promo_expired")]
    [InlineData("exhausted", "promo_exhausted")]
    public async Task APromotionThatCannotBeUsed_CannotBeSent(string kind, string problem)
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var promo = c.Promos.All.Single();
        if (kind == "inactive") promo.IsActive = false;
        if (kind == "expired") promo.ValidTo = _clock.GetUtcNow().UtcDateTime.AddDays(-1);
        if (kind == "exhausted") promo.UsedCount = 100;

        var preview = await c.Service.PreviewAsync(1, "en");

        Assert.False(preview.CanSend);
        Assert.Contains(problem, preview.Problems);
        await Assert.ThrowsAsync<BusinessRuleException>(() => c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99));
    }

    [Fact]
    public async Task WithoutSubscribers_NothingCanBeSent()
    {
        var c = CreateCampaigns(null, Sub(1, "pending@example.com", NewsletterStatus.Pending));

        var preview = await c.Service.PreviewAsync(1, "en");

        Assert.Contains("no_subscribers", preview.Problems);
    }

    [Fact]
    public async Task SavingOrEditingAPromotion_SendsNoEmail_AndCreatingACampaignOnlyQueuesIt()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"), Sub(2, "b@example.com"));

        var campaign = await QueueAsync(c);

        Assert.Equal(CampaignStatus.Queued, campaign.Status);
        Assert.Equal(2, campaign.TotalRecipients);
        Assert.Equal(2, campaign.Pending);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task AnUnconfirmedRequest_OrAStalePreview_IsRefused()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var preview = await c.Service.PreviewAsync(1, "en");

        var notConfirmed = await Assert.ThrowsAsync<BusinessRuleException>(() => c.Service.CreateAsync(1, new CreateCampaignRequestDto { PreviewHash = preview.PreviewHash, Confirm = false }, 99));
        Assert.Equal("campaign_not_confirmed", notConfirmed.Code);

        // The discount is read from the database, never from the request: a changed promotion needs a new preview.
        c.Promos.All.Single().DiscountPercent = 50;
        await Assert.ThrowsAsync<ConflictException>(() => c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99));
        await Assert.ThrowsAsync<ConflictException>(() => c.Service.CreateAsync(1, Confirm("made-up"), 99));
        Assert.Empty(c.Repository.All);
    }

    [Fact]
    public async Task APromoCode_HasOneCampaign_ASecondConfirmationIsRefused()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var preview = await c.Service.PreviewAsync(1, "en");
        await c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99);

        await Assert.ThrowsAsync<ConflictException>(() => c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99));
        Assert.Single(c.Repository.All);
        Assert.Contains("campaign_already_sent", (await c.Service.PreviewAsync(1, "en")).Problems);
    }

    [Fact]
    public async Task TheCampaign_GoesOnlyToConfirmedSubscribers_InTheirLanguage_WithTheirOwnUnsubscribeLink()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com", language: "en"), Sub(2, "b@example.com", language: "az"), Sub(3, "pending@example.com", NewsletterStatus.Pending),
            Sub(4, "left@example.com", NewsletterStatus.Unsubscribed), Sub(5, "r@example.com", language: "ru"));
        await QueueAsync(c);

        var sent = await c.Service.ProcessDueAsync();

        Assert.Equal(3, sent);
        Assert.Equal(new[] { "a@example.com", "b@example.com", "r@example.com" }, _sender.Sent.Select(m => m.To));
        Assert.Contains("20% off", _sender.Sent[0].Subject);
        Assert.Contains("20% endirim", _sender.Sent[1].Subject);
        Assert.Contains("скидка 20%", _sender.Sent[2].Subject);
        Assert.All(_sender.Sent, m =>
        {
            Assert.Contains("WINTER20", m.TextBody);
            Assert.Contains("newsletter-unsubscribe.html?token=", m.TextBody);
            Assert.Contains("newsletter-unsubscribe.html?token=", m.HtmlBody);
            Assert.Contains("https://aerova.test/booking.html", m.TextBody);
        });
        Assert.Equal(3, _sender.Sent.Select(m => m.TextBody.Split("token=")[^1].Split('\n')[0]).Distinct().Count());
    }

    [Fact]
    public async Task TheEmail_ShowsTheExpiryAndTheUsageLimit_OfTheSavedPromotion()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        c.Promos.All.Single().ValidTo = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await QueueAsync(c);

        await c.Service.ProcessDueAsync();

        var text = _sender.Sent.Single().TextBody;
        Assert.Contains("Valid until 2026-12-31", text);
        Assert.Contains("Limited number of uses: 100", text);
    }

    [Fact]
    public async Task ACampaign_IsSentInBatches_AndNobodyGetsItTwice()
    {
        var c = CreateCampaigns(new NewsletterSettings { CampaignBatchSize = 2 }, Sub(1, "a@example.com"), Sub(2, "b@example.com"), Sub(3, "c@example.com"));
        var campaign = await QueueAsync(c);

        Assert.Equal(2, await c.Service.ProcessDueAsync());
        Assert.Equal(CampaignStatus.Sending, (await c.Service.GetAsync(campaign.Id)).Status);
        Assert.Equal(1, await c.Service.ProcessDueAsync());
        Assert.Equal(0, await c.Service.ProcessDueAsync());

        var done = await c.Service.GetAsync(campaign.Id);
        Assert.Equal(CampaignStatus.Completed, done.Status);
        Assert.Equal(3, done.Sent);
        Assert.Equal(0, done.Pending);
        Assert.Equal(3, _sender.Sent.Select(m => m.To).Distinct().Count());
        Assert.NotNull(done.CompletedAt);
    }

    [Fact]
    public async Task SomeoneWhoUnsubscribesAfterTheCampaignWasQueued_GetsNothing()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"), Sub(2, "b@example.com"));
        var campaign = await QueueAsync(c);
        c.Subscribers.All.Single(s => s.Id == 2).Status = NewsletterStatus.Unsubscribed;

        await c.Service.ProcessDueAsync();

        Assert.Equal("a@example.com", Assert.Single(_sender.Sent).To);
        var done = await c.Service.GetAsync(campaign.Id);
        Assert.Equal(1, done.Sent);
        Assert.Equal(1, done.Skipped);
        Assert.Equal(CampaignStatus.Completed, done.Status);
    }

    [Fact]
    public async Task WhenTheMailServerFails_RecipientsAreRetried_AndMarkedFailedAfterTheAllowedAttempts()
    {
        var c = CreateCampaigns(new NewsletterSettings { CampaignMaxAttempts = 2 }, Sub(1, "a@example.com"), Sub(2, "b@example.com"));
        var campaign = await QueueAsync(c);
        _sender.Fail = true;

        await c.Service.ProcessDueAsync();
        var afterFirst = await c.Service.GetAsync(campaign.Id);
        Assert.Equal(2, afterFirst.Pending);
        Assert.Equal(CampaignStatus.Sending, afterFirst.Status);

        await c.Service.ProcessDueAsync();
        var done = await c.Service.GetAsync(campaign.Id);

        Assert.Equal(2, done.Failed);
        Assert.Equal(0, done.Sent);
        Assert.Equal(CampaignStatus.CompletedWithErrors, done.Status);
        Assert.Empty(_sender.Sent);
        Assert.All(c.Repository.All[0].Recipients, r => Assert.Equal("delivery_failed", r.LastError));
        Assert.Contains(_alerts.All, a => a.Subject == "Email campaign finished" && a.Body.Contains("2 failed"));
    }

    [Fact]
    public async Task ARecipientThatFailedOnce_IsDeliveredOnTheNextRun_WithoutDuplicatesForTheOthers()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"), Sub(2, "b@example.com"));
        var campaign = await QueueAsync(c);
        _sender.Fail = true;
        await c.Service.ProcessDueAsync();

        _sender.Fail = false;
        await c.Service.ProcessDueAsync();
        await c.Service.ProcessDueAsync();

        var done = await c.Service.GetAsync(campaign.Id);
        Assert.Equal(CampaignStatus.Completed, done.Status);
        Assert.Equal(2, done.Sent);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task WithoutMailSettings_NoCampaignEmailIsSent_AndNoAttemptIsUsed()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        await QueueAsync(c);
        _sender.IsConfigured = false;

        Assert.Equal(0, await c.Service.ProcessDueAsync());

        Assert.Equal(0, c.Repository.All[0].Recipients[0].Attempts);
        Assert.Equal(CampaignStatus.Queued, c.Repository.All[0].Status);
    }

    [Fact]
    public async Task AFinishedCampaign_AlertsTheAdminWithTheOutcome_AndCannotBeSentAgain()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var campaign = await QueueAsync(c);
        await c.Service.ProcessDueAsync();

        var alert = Assert.Single(_alerts.All);
        Assert.Contains("WINTER20", alert.Body);
        Assert.Contains("1 sent", alert.Body);

        var preview = await c.Service.PreviewAsync(1, "en");
        await Assert.ThrowsAsync<ConflictException>(() => c.Service.CreateAsync(1, Confirm(preview.PreviewHash), 99));
        Assert.Equal(1, (await c.Service.GetAsync(campaign.Id)).Sent);
    }

    private sealed class FakeDbException : System.Data.Common.DbException
    {
        public FakeDbException(string message) : base(message)
        {
        }
    }

    [Fact]
    public async Task AMissingCampaignTable_BecomesAClearSetupMessage_WithTheCode_NotAGenericFailure()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var broken = new CampaignService(c.Promos, new MissingTableCampaignRepository(), c.Subscribers, null!, CreateAlerts(), _sender,
            Options.Create(new NewsletterSettings()), Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" }), _clock, NullLogger<CampaignService>.Instance);

        var preview = await Assert.ThrowsAsync<BusinessRuleException>(() => broken.PreviewAsync(1, "en"));
        var history = await Assert.ThrowsAsync<BusinessRuleException>(() => broken.GetRecentAsync());
        var single = await Assert.ThrowsAsync<BusinessRuleException>(() => broken.GetAsync(1));

        Assert.All(new[] { preview, history, single }, e =>
        {
            Assert.Equal("campaign_setup_required", e.Code);
            Assert.Equal(CampaignService.SetupRequiredMessage, e.Message);
            Assert.DoesNotContain("EmailCampaigns'", e.Message);
        });
    }

    [Fact]
    public void ADatabaseThatLacksATable_IsRecognised_AndAnOrdinaryFailureIsNot()
    {
        Assert.True(Service.Helpers.DatabaseSetup.IsMissingTable(new FakeDbException("Invalid object name 'SupportTickets'.")));
        Assert.True(Service.Helpers.DatabaseSetup.IsMissingTable(new InvalidOperationException("outer", new FakeDbException("Invalid column name 'Status'."))));
        Assert.False(Service.Helpers.DatabaseSetup.IsMissingTable(new InvalidOperationException("Invalid object name 'x' in a message that is not a database error.")));
        Assert.False(Service.Helpers.DatabaseSetup.IsMissingTable(new FakeDbException("Cannot insert duplicate key.")));
        Assert.DoesNotContain("SupportTickets", Service.Helpers.DatabaseSetup.RequiredMessage);
    }

    [Fact]
    public async Task AnotherFailure_IsNotMistakenForAMissingTable()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));
        var broken = new CampaignService(c.Promos, new ThrowingCampaignRepository(new InvalidOperationException("something else")), c.Subscribers, null!, CreateAlerts(), _sender,
            Options.Create(new NewsletterSettings()), Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" }), _clock, NullLogger<CampaignService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => broken.GetRecentAsync());
        Assert.False(CampaignService.IsMissingTable(new InvalidOperationException("something else")));
        Assert.True(CampaignService.IsMissingTable(new InvalidOperationException("outer", new FakeDbException("Invalid object name 'EmailCampaigns'."))));
        Assert.True(CampaignService.IsMissingTable(new FakeDbException("Invalid column name 'Status'.")));
    }

    private class ThrowingCampaignRepository : FakeEmailCampaignRepository
    {
        private readonly Exception _exception;

        public ThrowingCampaignRepository(Exception exception) => _exception = exception;

        public override Task<EmailCampaign?> GetByPromoCodeIdAsync(int promoCodeId, CancellationToken cancellationToken = default) => throw _exception;

        public override Task<IReadOnlyList<EmailCampaign>> GetRecentAsync(int take, CancellationToken cancellationToken = default) => throw _exception;

        public override Task<EmailCampaign?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw _exception;
    }

    private sealed class MissingTableCampaignRepository : ThrowingCampaignRepository
    {
        public MissingTableCampaignRepository() : base(new FakeDbException("Invalid object name 'EmailCampaigns'."))
        {
        }
    }

    [Fact]
    public async Task AnUnknownPromoCode_OrCampaign_IsNotFound()
    {
        var c = CreateCampaigns(null, Sub(1, "a@example.com"));

        await Assert.ThrowsAsync<NotFoundException>(() => c.Service.PreviewAsync(99, "en"));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Service.GetAsync(99));
    }
}
