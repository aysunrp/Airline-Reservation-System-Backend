using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Newsletter;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A test API where each IP address may make three newsletter requests a minute.</summary>
public sealed class NewsletterLimitedApiFactory : ApiFactory
{
    public NewsletterLimitedApiFactory()
    {
        NewsletterRateLimitPerMinute = 3;
    }
}

/// <summary>The newsletter subscription over HTTP, as the home page and the two e-mail link pages call it. The mail server is a stand-in.</summary>
public class NewsletterEndpointTests : IClassFixture<ApiFactory>, IClassFixture<NewsletterLimitedApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly NewsletterLimitedApiFactory _limited;

    public NewsletterEndpointTests(ApiFactory factory, NewsletterLimitedApiFactory limited)
    {
        _factory = factory;
        _limited = limited;
        _factory.Emails.Fail = false;
        _factory.Emails.IsConfigured = true;
    }

    private static string TokenAfter(string text, string page)
    {
        var start = text.LastIndexOf(page + "?token=", StringComparison.Ordinal) + (page + "?token=").Length;
        var end = text.IndexOfAny(new[] { '\n', ' ', '\r' }, start);
        return Uri.UnescapeDataString(text[start..(end < 0 ? text.Length : end)]);
    }

    private Task<HttpResponseMessage> PostAsync(string path, object body, HttpClient? client = null)
        => (client ?? _factory.CreateClient()).PostAsJsonAsync("/api/newsletter/" + path, body, Json);

    private string Latest(string email, string page)
        => TokenAfter(_factory.Emails.Sent.Last(m => m.To == email).TextBody, page);

    [Fact]
    public async Task ASubscription_NeedsNoLogin_AndSendsTheConfirmationEmail()
    {
        var email = UniqueEmail("news");

        var response = await PostAsync("subscribe", new { email, language = "az" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("confirmation_sent", (await response.ReadAsync<NewsletterSubscribeResponseDto>()).Status);
        var message = Assert.Single(_factory.Emails.Sent, m => m.To == email);
        Assert.Contains("https://aerova.test/newsletter-confirm.html?token=", message.TextBody);
        Assert.Contains("newsletter-unsubscribe.html?token=", message.HtmlBody);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public async Task AnInvalidAddress_Is400_AndNothingIsSent(string email)
    {
        var before = _factory.Emails.Sent.Count;

        var response = await PostAsync("subscribe", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task TheWholeFlow_Subscribe_Confirm_Unsubscribe()
    {
        var email = UniqueEmail("flow");
        await PostAsync("subscribe", new { email });

        var confirm = await PostAsync("confirm", new { token = Latest(email, "newsletter-confirm.html") });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("confirmed", (await confirm.ReadAsync<NewsletterResultDto>()).Status);

        var token = Latest(email, "newsletter-unsubscribe.html");
        var unsubscribe = await PostAsync("unsubscribe", new { token });
        Assert.Equal(HttpStatusCode.OK, unsubscribe.StatusCode);
        Assert.Equal("unsubscribed", (await unsubscribe.ReadAsync<NewsletterResultDto>()).Status);

        // Opening it again changes nothing and is not an error.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("unsubscribe", new { token })).StatusCode);
    }

    [Fact]
    public async Task AConfirmationLink_WorksOnce_AnInvalidOneIsRefusedWithACode()
    {
        var email = UniqueEmail("once");
        await PostAsync("subscribe", new { email });
        var token = Latest(email, "newsletter-confirm.html");

        Assert.Equal(HttpStatusCode.OK, (await PostAsync("confirm", new { token })).StatusCode);

        var reused = await PostAsync("confirm", new { token });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("link_invalid", (await reused.ReadJsonAsync()).GetProperty("code").GetString());

        var unknown = await PostAsync("confirm", new { token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
    }

    [Fact]
    public async Task ABadUnsubscribeLink_IsRefused_AndDoesNotOptAnyoneOut()
    {
        var email = UniqueEmail("bad");
        await PostAsync("subscribe", new { email });
        await PostAsync("confirm", new { token = Latest(email, "newsletter-confirm.html") });
        var good = Latest(email, "newsletter-unsubscribe.html");

        var forged = await PostAsync("unsubscribe", new { token = good.Split('.')[0] + ".AAAAAAAAAAAAAAAAAAAAAA" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forged.StatusCode);
        Assert.Equal("link_invalid", (await forged.ReadJsonAsync()).GetProperty("code").GetString());

        // The real link still works: the forged request changed nothing.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("unsubscribe", new { token = good })).StatusCode);
    }

    [Fact]
    public async Task WhenTheMailServerFails_TheVisitorGets503_NotAFalseSuccess()
    {
        var email = UniqueEmail("smtp");
        _factory.Emails.Fail = true;

        var response = await PostAsync("subscribe", new { email });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == email);

        _factory.Emails.Fail = false;
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("subscribe", new { email })).StatusCode);
        Assert.Contains(_factory.Emails.Sent, m => m.To == email);
    }

    [Fact]
    public async Task TheSameAnswerForANewAndAnAlreadySubscribedAddress()
    {
        var email = UniqueEmail("same");
        var first = await (await PostAsync("subscribe", new { email })).ReadJsonAsync();
        await PostAsync("confirm", new { token = Latest(email, "newsletter-confirm.html") });

        var again = await (await PostAsync("subscribe", new { email })).ReadJsonAsync();

        Assert.Equal(first.ToString(), again.ToString());
    }

    [Fact]
    public async Task RepeatedRequests_AreLimitedPerCaller()
    {
        var client = _limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await PostAsync("subscribe", new { email = UniqueEmail("limit") }, client)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync("subscribe", new { email = UniqueEmail("limit") }, client)).StatusCode);

        // Confirmation and unsubscribe share the limit.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync("confirm", new { token = "x" }, client)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync("unsubscribe", new { token = "x" }, client)).StatusCode);
    }
}
