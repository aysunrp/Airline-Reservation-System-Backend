using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

internal sealed class FakeNewsletterSubscriberRepository : FakeRepository<NewsletterSubscriber>, INewsletterSubscriberRepository
{
    public FakeNewsletterSubscriberRepository(params NewsletterSubscriber[] items) : base(items)
    {
    }

    public IReadOnlyList<NewsletterSubscriber> All => Items;

    public Task<NewsletterSubscriber?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.Email == normalizedEmail));

    public Task<NewsletterSubscriber?> GetByConfirmationHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.ConfirmationTokenHash == tokenHash));

    public Task<NewsletterSubscriber?> GetByUnsubscribeKeyAsync(Guid key, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.UnsubscribeKey == key));

    public Task<IReadOnlyList<NewsletterSubscriber>> GetActiveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NewsletterSubscriber>>(Items.Where(s => s.Status == NewsletterStatus.Active).ToList());
}

internal sealed class MovableClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Newsletter subscription with e-mail confirmation and unsubscribe. The mail server is always a stand-in: no real e-mail is sent.</summary>
public class NewsletterTests
{
    private readonly FakeNewsletterSubscriberRepository _repository = new();
    private readonly FakeEmailSender _sender = new();
    private readonly MovableClock _clock = new();

    private NewsletterService CreateService(NewsletterSettings? settings = null)
        => new(_repository, _sender, new NewsletterSubscribeRequestValidator(), new NewsletterTokenRequestValidator(),
            Options.Create(settings ?? new NewsletterSettings()), Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test" }),
            Options.Create(new JwtSettings { Secret = "newsletter-tests-secret-newsletter-tests-secret" }), _clock, NullLogger<NewsletterService>.Instance);

    private static NewsletterSubscribeRequestDto Request(string email, string? language = "en") => new() { Email = email, Language = language };

    private static string TokenAfter(string text, string page)
    {
        var start = text.IndexOf(page + "?token=", StringComparison.Ordinal) + (page + "?token=").Length;
        var end = text.IndexOfAny(new[] { '\n', ' ', '\r' }, start);
        return Uri.UnescapeDataString(text[start..(end < 0 ? text.Length : end)]);
    }

    private string ConfirmToken(int message = -1) => TokenAfter(_sender.Sent[message < 0 ? _sender.Sent.Count - 1 : message].TextBody, "newsletter-confirm.html");

    private string UnsubscribeToken(int message = -1) => TokenAfter(_sender.Sent[message < 0 ? _sender.Sent.Count - 1 : message].TextBody, "newsletter-unsubscribe.html");

    private static NewsletterTokenRequestDto Token(string token) => new() { Token = token };

    private async Task<NewsletterService> SubscribedAndConfirmedAsync(string email = "ayla@example.com")
    {
        var service = CreateService();
        await service.SubscribeAsync(Request(email));
        await service.ConfirmAsync(Token(ConfirmToken()));
        return service;
    }

    // ---- subscribing ----

    [Fact]
    public async Task ANewAddress_IsStoredAsPending_AndGetsOneConfirmationEmail()
    {
        var response = await CreateService().SubscribeAsync(Request("  Ayla@Example.COM "));

        Assert.Equal("confirmation_sent", response.Status);
        var subscriber = Assert.Single(_repository.All);
        Assert.Equal("ayla@example.com", subscriber.Email);
        Assert.Equal(NewsletterStatus.Pending, subscriber.Status);
        Assert.Null(subscriber.ConfirmedAt);
        Assert.NotNull(subscriber.ConfirmationSentAt);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddHours(48), subscriber.ConfirmationExpiresAt);

        var message = Assert.Single(_sender.Sent);
        Assert.Equal("ayla@example.com", message.To);
    }

    [Fact]
    public async Task TheConfirmationEmail_HasAButton_AFallbackUrl_AndAnUnsubscribeLink_AndNoInternalIds()
    {
        await CreateService().SubscribeAsync(Request("ayla@example.com"));

        var message = Assert.Single(_sender.Sent);
        var token = ConfirmToken();
        var link = "https://aerova.test/newsletter-confirm.html?token=" + Uri.EscapeDataString(token);
        Assert.Contains(link, message.TextBody);
        Assert.Contains($"href=\"{link}\"", message.HtmlBody);
        Assert.Contains(">Confirm subscription<", message.HtmlBody);
        Assert.Contains("newsletter-unsubscribe.html?token=", message.TextBody);
        Assert.Contains("newsletter-unsubscribe.html?token=", message.HtmlBody);
        Assert.DoesNotContain("id=", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ayla@example.com", message.TextBody);
        Assert.Contains("48 hours", message.TextBody);
    }

    [Fact]
    public async Task OnlyAHashOfTheConfirmationToken_IsStored()
    {
        await CreateService().SubscribeAsync(Request("ayla@example.com"));

        var stored = _repository.All[0].ConfirmationTokenHash!;
        Assert.Equal(64, stored.Length);
        Assert.NotEqual(ConfirmToken(), stored);
        Assert.True(ConfirmToken().Length >= 43, "256 random bits");
        Assert.DoesNotContain(ConfirmToken(), stored);
    }

    [Fact]
    public async Task TheEmailsFollowTheLanguageOfThePage()
    {
        await CreateService().SubscribeAsync(Request("a@example.com", "az"));
        await CreateService().SubscribeAsync(Request("b@example.com", "ru"));
        await CreateService().SubscribeAsync(Request("c@example.com", "xx"));

        Assert.Equal("AEROVA abunəliyinizi təsdiqləyin", _sender.Sent[0].Subject);
        Assert.Equal("Подтвердите подписку на AEROVA", _sender.Sent[1].Subject);
        Assert.Equal("Confirm your AEROVA subscription", _sender.Sent[2].Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.com")]
    [InlineData("two@@example.com")]
    public async Task AnInvalidAddress_IsRefused_NothingIsStoredOrSent(string email)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateService().SubscribeAsync(Request(email)));

        Assert.Empty(_repository.All);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task ATooLongAddress_IsRefused()
    {
        var email = new string('a', 250) + "@example.com";

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().SubscribeAsync(Request(email)));
    }

    [Fact]
    public async Task ARepeatedRequest_AMomentLater_SendsNoSecondEmail_AndStoresNoSecondRow()
    {
        var service = CreateService();

        var first = await service.SubscribeAsync(Request("ayla@example.com"));
        var second = await service.SubscribeAsync(Request("AYLA@example.com"));

        Assert.Equal(first, second);
        Assert.Single(_repository.All);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task ARepeatedRequest_AfterTheWait_SendsANewLink_AndTheOldOneStopsWorking()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("ayla@example.com"));
        var oldToken = ConfirmToken();

        _clock.Advance(TimeSpan.FromMinutes(3));
        await service.SubscribeAsync(Request("ayla@example.com"));

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Single(_repository.All);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(Token(oldToken)));
        Assert.Equal("link_invalid", ex.Code);
        Assert.Equal("confirmed", (await service.ConfirmAsync(Token(ConfirmToken()))).Status);
    }

    // ---- confirming ----

    [Fact]
    public async Task OpeningTheConfirmationLink_ActivatesTheSubscription()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("ayla@example.com"));

        var result = await service.ConfirmAsync(Token(ConfirmToken()));

        Assert.Equal("confirmed", result.Status);
        var subscriber = _repository.All[0];
        Assert.Equal(NewsletterStatus.Active, subscriber.Status);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, subscriber.ConfirmedAt);
        Assert.Null(subscriber.ConfirmationTokenHash);
    }

    [Fact]
    public async Task TheSubscription_IsNotActive_UntilTheLinkIsOpened()
    {
        await CreateService().SubscribeAsync(Request("ayla@example.com"));

        Assert.Equal(NewsletterStatus.Pending, _repository.All[0].Status);
        Assert.Empty(await CreateService().GetRecipientsAsync());
    }

    [Fact]
    public async Task AConfirmationLink_WorksOnce()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("ayla@example.com"));
        var token = ConfirmToken();
        await service.ConfirmAsync(Token(token));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(Token(token)));

        Assert.Equal("link_invalid", ex.Code);
        Assert.Equal(NewsletterStatus.Active, _repository.All[0].Status);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AnUnknownConfirmationLink_IsRefused(string token)
    {
        await CreateService().SubscribeAsync(Request("ayla@example.com"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ConfirmAsync(Token(token)));

        Assert.Equal("link_invalid", ex.Code);
        Assert.Equal(NewsletterStatus.Pending, _repository.All[0].Status);
    }

    [Fact]
    public async Task AnEmptyOrHugeConfirmationToken_IsRefusedAsInvalidInput()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(Token("")));
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(Token(new string('x', 500))));
    }

    [Fact]
    public async Task AnExpiredConfirmationLink_IsRefused_AndASafeNewRequestIsPossible()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("ayla@example.com"));
        var token = ConfirmToken();

        _clock.Advance(TimeSpan.FromHours(49));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(Token(token)));
        Assert.Equal("link_expired", ex.Code);
        Assert.Equal(NewsletterStatus.Pending, _repository.All[0].Status);

        await service.SubscribeAsync(Request("ayla@example.com"));
        Assert.Equal(2, _sender.Sent.Count);
        Assert.Equal("confirmed", (await service.ConfirmAsync(Token(ConfirmToken()))).Status);
    }

    // ---- the mail server fails ----

    [Fact]
    public async Task WhenTheEmailCannotBeSent_ItIsNotReportedAsSent_AndTheNextRequestTriesAgainAtOnce()
    {
        var service = CreateService();
        _sender.Fail = true;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.SubscribeAsync(Request("ayla@example.com")));

        Assert.Empty(_sender.Sent);
        Assert.Null(_repository.All[0].ConfirmationSentAt);
        Assert.Equal(NewsletterStatus.Pending, _repository.All[0].Status);

        _sender.Fail = false;
        var response = await service.SubscribeAsync(Request("ayla@example.com"));

        Assert.Equal("confirmation_sent", response.Status);
        Assert.Single(_sender.Sent);
        Assert.NotNull(_repository.All[0].ConfirmationSentAt);
    }

    [Fact]
    public async Task WithoutMailSettings_NothingIsStored_AndTheVisitorIsToldItIsUnavailable()
    {
        _sender.IsConfigured = false;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => CreateService().SubscribeAsync(Request("ayla@example.com")));

        Assert.Empty(_repository.All);
    }

    // ---- already subscribed ----

    [Fact]
    public async Task AnAddressThatIsAlreadyActive_GetsTheSameAnswer_AndOnlyAnInformationEmail()
    {
        var service = await SubscribedAndConfirmedAsync();
        var before = _sender.Sent.Count;

        var response = await service.SubscribeAsync(Request("ayla@example.com"));

        Assert.Equal("confirmation_sent", response.Status);
        Assert.Equal(NewsletterStatus.Active, _repository.All[0].Status);
        Assert.Null(_repository.All[0].ConfirmationTokenHash);
        Assert.Equal(before + 1, _sender.Sent.Count);
        var info = _sender.Sent[^1];
        Assert.DoesNotContain("newsletter-confirm.html", info.TextBody);
        Assert.Contains("newsletter-unsubscribe.html?token=", info.TextBody);
    }

    // ---- unsubscribing ----

    [Fact]
    public async Task TheUnsubscribeLink_OptsTheAddressOut_WithoutALogin()
    {
        var service = await SubscribedAndConfirmedAsync();
        var token = UnsubscribeToken(0);

        var result = await service.UnsubscribeAsync(Token(token));

        Assert.Equal("unsubscribed", result.Status);
        Assert.Equal(NewsletterStatus.Unsubscribed, _repository.All[0].Status);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, _repository.All[0].UnsubscribedAt);
    }

    [Fact]
    public async Task Unsubscribing_TwiceIsHarmless()
    {
        var service = await SubscribedAndConfirmedAsync();
        var token = UnsubscribeToken(0);

        await service.UnsubscribeAsync(Token(token));
        var again = await service.UnsubscribeAsync(Token(token));

        Assert.Equal("unsubscribed", again.Status);
        Assert.Equal(NewsletterStatus.Unsubscribed, _repository.All[0].Status);
    }

    [Fact]
    public async Task AnUnsubscribeLink_WithABadSignature_OrAGuessedKey_IsRefused()
    {
        var service = await SubscribedAndConfirmedAsync();
        var good = UnsubscribeToken(0);
        var key = good.Split('.')[0];

        foreach (var bad in new[]
                 {
                     key + ".AAAAAAAAAAAAAAAAAAAAAA",
                     key,
                     Guid.NewGuid().ToString("N") + "." + good.Split('.')[1],
                     key.ToUpperInvariant() + "." + good.Split('.')[1][..^1] + "x",
                     "not-a-token",
                     key + "." + good.Split('.')[1] + ".extra"
                 })
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UnsubscribeAsync(Token(bad)));
            Assert.Equal("link_invalid", ex.Code);
        }

        Assert.Equal(NewsletterStatus.Active, _repository.All[0].Status);
    }

    [Fact]
    public async Task OneSubscribersLink_NeverOptsOutAnotherSubscriber()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("a@example.com"));
        await service.ConfirmAsync(Token(ConfirmToken()));
        await service.SubscribeAsync(Request("b@example.com"));
        await service.ConfirmAsync(Token(ConfirmToken()));

        await service.UnsubscribeAsync(Token(UnsubscribeToken(0)));

        Assert.Equal(NewsletterStatus.Unsubscribed, _repository.All.Single(s => s.Email == "a@example.com").Status);
        Assert.Equal(NewsletterStatus.Active, _repository.All.Single(s => s.Email == "b@example.com").Status);
    }

    [Fact]
    public async Task AnUnsubscribedAddress_StaysOut_UntilItSubscribesAgainAndConfirms()
    {
        var service = await SubscribedAndConfirmedAsync();
        await service.UnsubscribeAsync(Token(UnsubscribeToken(0)));
        Assert.Empty(await service.GetRecipientsAsync());

        // Asking again sends a new confirmation but does not bring the address back.
        _clock.Advance(TimeSpan.FromMinutes(5));
        await service.SubscribeAsync(Request("ayla@example.com"));

        Assert.Equal(NewsletterStatus.Pending, _repository.All[0].Status);
        Assert.Empty(await service.GetRecipientsAsync());

        await service.ConfirmAsync(Token(ConfirmToken()));
        Assert.Single(await service.GetRecipientsAsync());
    }

    [Fact]
    public async Task AConfirmationLinkSentBeforeUnsubscribing_CannotBringTheAddressBack()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("ayla@example.com"));
        var confirm = ConfirmToken();
        var unsubscribe = UnsubscribeToken();

        await service.UnsubscribeAsync(Token(unsubscribe));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(Token(confirm)));
        Assert.Equal("link_invalid", ex.Code);
        Assert.Equal(NewsletterStatus.Unsubscribed, _repository.All[0].Status);
    }

    // ---- who may receive a newsletter ----

    [Fact]
    public async Task OnlyConfirmedActiveSubscribers_AreRecipients_EachWithAnUnsubscribeLink()
    {
        var service = CreateService();
        await service.SubscribeAsync(Request("pending@example.com"));
        await service.SubscribeAsync(Request("active@example.com", "az"));
        await service.ConfirmAsync(Token(ConfirmToken()));
        await service.SubscribeAsync(Request("left@example.com"));
        await service.ConfirmAsync(Token(ConfirmToken()));
        await service.UnsubscribeAsync(Token(UnsubscribeToken()));

        var recipients = await service.GetRecipientsAsync();

        var only = Assert.Single(recipients);
        Assert.Equal("active@example.com", only.Email);
        Assert.Equal("az", only.Language);
        Assert.StartsWith("https://aerova.test/newsletter-unsubscribe.html?token=", only.UnsubscribeLink);

        // The link of a recipient really opts that recipient out, and then nothing more goes to them.
        var token = Uri.UnescapeDataString(only.UnsubscribeLink[(only.UnsubscribeLink.IndexOf("?token=", StringComparison.Ordinal) + 7)..]);
        await service.UnsubscribeAsync(Token(token));
        Assert.Empty(await service.GetRecipientsAsync());
    }

    [Fact]
    public async Task NoNewsletterIsSentBySubscribing_OnlyConfirmationAndInformationEmails()
    {
        var service = await SubscribedAndConfirmedAsync();
        await service.SubscribeAsync(Request("ayla@example.com"));

        Assert.All(_sender.Sent, m => Assert.Contains("newsletter-unsubscribe.html", m.TextBody));
        Assert.Equal(2, _sender.Sent.Count);
    }
}
