using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Promotion e-mail campaigns over HTTP: saving a promo code sends nothing, an admin previews and confirms, the background job (run here by hand)
/// sends to confirmed subscribers only. The mail server is a stand-in: no real e-mail is sent.
/// </summary>
public class CampaignEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CampaignEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.Fail = false;
        _factory.Emails.IsConfigured = true;
    }

    private static string TokenAfter(string text, string page)
    {
        var start = text.LastIndexOf(page + "?token=", StringComparison.Ordinal) + (page + "?token=").Length;
        var end = text.IndexOfAny(new[] { '\n', ' ', '\r' }, start);
        return Uri.UnescapeDataString(text[start..(end < 0 ? text.Length : end)]);
    }

    private async Task<string> SubscribeAsync(string email, bool confirm = true)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/newsletter/subscribe", new { email, language = "en" }, Json);
        if (confirm)
        {
            var token = TokenAfter(_factory.Emails.Sent.Last(m => m.To == email).TextBody, "newsletter-confirm.html");
            await client.PostAsJsonAsync("/api/newsletter/confirm", new { token }, Json);
        }

        return email;
    }

    private static async Task<AdminPromoCodeDto> CreatePromoAsync(HttpClient admin, string code, int percent = 15)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/promo-codes",
            new { code, discountPercent = percent, validTo = DateTime.UtcNow.AddDays(20), maxUses = 50, isActive = true }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<AdminPromoCodeDto>();
    }

    private async Task<int> RunBackgroundJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICampaignService>().ProcessDueAsync();
    }

    [Fact]
    public async Task SavingAPromoCode_SendsNoEmail()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var subscriber = await SubscribeAsync($"quiet.{Guid.NewGuid():N}@example.com");
        var before = _factory.Emails.Sent.Count;

        var promo = await CreatePromoAsync(admin, "QUIET" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant());
        await admin.PutAsJsonAsync($"/api/admin/promo-codes/{promo.Id}", new { code = promo.Code, discountPercent = 25, isActive = true }, Json);
        await RunBackgroundJobAsync();

        Assert.Equal(before, _factory.Emails.Sent.Count);
        Assert.DoesNotContain(_factory.Emails.Sent.Skip(before), m => m.To == subscriber);
    }

    [Fact]
    public async Task OnlyAnAdmin_CanPreviewOrSendACampaign()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var promo = await CreatePromoAsync(admin, "AUTH" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant());

        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/admin/promo-codes/{promo.Id}/campaign-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = "x", confirm = true }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/campaigns")).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync(UniqueEmail("customer"))).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/promo-codes/{promo.Id}/campaign-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = "x", confirm = true }, Json)).StatusCode);
    }

    [Fact]
    public async Task PreviewConfirmSend_GoesToConfirmedSubscribersOnly_AndCannotBeSentTwice()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var confirmed = await SubscribeAsync($"yes.{Guid.NewGuid():N}@example.com");
        var pending = await SubscribeAsync($"wait.{Guid.NewGuid():N}@example.com", confirm: false);
        var left = await SubscribeAsync($"left.{Guid.NewGuid():N}@example.com");
        var code = "SEND" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var promo = await CreatePromoAsync(admin, code, 15);

        // Unsubscribe one of them before the campaign.
        var unsubscribeToken = TokenAfter(_factory.Emails.Sent.Last(m => m.To == left).TextBody, "newsletter-unsubscribe.html");
        await _factory.CreateClient().PostAsJsonAsync("/api/newsletter/unsubscribe", new { token = unsubscribeToken }, Json);

        var previewResponse = await admin.GetAsync($"/api/admin/promo-codes/{promo.Id}/campaign-preview?language=en");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.ReadAsync<CampaignPreviewDto>();
        Assert.True(preview.CanSend);
        Assert.Equal(code, preview.Code);
        Assert.Equal(15, preview.DiscountPercent);
        Assert.Contains(code, preview.Text);

        // Nothing is sent by the preview.
        var before = _factory.Emails.Sent.Count;

        // The request cannot carry a discount: a wrong hash is refused.
        var stale = await admin.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = "stale", confirm = true }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var unconfirmed = await admin.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = preview.PreviewHash, confirm = false }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unconfirmed.StatusCode);
        Assert.Equal(before, _factory.Emails.Sent.Count);

        var created = await admin.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = preview.PreviewHash, confirm = true }, Json);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var campaign = await created.ReadAsync<CampaignDto>();
        Assert.Equal(Domain.Enums.CampaignStatus.Queued, campaign.Status);
        Assert.Equal(before, _factory.Emails.Sent.Count);

        // The same promotion cannot be sent a second time.
        var again = await admin.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = preview.PreviewHash, confirm = true }, Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        await RunBackgroundJobAsync();
        await RunBackgroundJobAsync();

        var promoMail = _factory.Emails.Sent.Skip(before).Where(m => m.TextBody.Contains(code)).ToList();
        Assert.Contains(promoMail, m => m.To == confirmed);
        Assert.DoesNotContain(promoMail, m => m.To == pending);
        Assert.DoesNotContain(promoMail, m => m.To == left);
        Assert.Equal(promoMail.Count, promoMail.Select(m => m.To).Distinct().Count());
        Assert.All(promoMail, m => Assert.Contains("newsletter-unsubscribe.html?token=", m.TextBody));

        var status = await (await admin.GetAsync($"/api/admin/campaigns/{campaign.Id}")).ReadAsync<CampaignDto>();
        Assert.Equal(Domain.Enums.CampaignStatus.Completed, status.Status);
        Assert.Equal(promoMail.Count, status.Sent);
        var list = await (await admin.GetAsync("/api/admin/campaigns")).ReadAsync<List<CampaignDto>>();
        Assert.Contains(list, c => c.Id == campaign.Id);
    }

    [Fact]
    public async Task APromoCodeThatDoesNotExist_Is404()
    {
        var admin = await _factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/promo-codes/999999/campaign-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/campaigns/999999")).StatusCode);
    }
}
