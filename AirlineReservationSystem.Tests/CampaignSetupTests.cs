using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Newsletter;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// The admin campaign page failed with a generic server error when the database did not have the campaign tables (the migration had not
/// been applied). The failing requests were GET /api/admin/campaigns and GET /api/admin/promo-codes/{id}/campaign-preview.
/// </summary>
public class CampaignSetupTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CampaignSetupTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.Fail = false;
        _factory.Emails.IsConfigured = true;
    }

    [Fact]
    public async Task OnADatabaseWithoutTheCampaignTables_TheEndpointsSayWhatIsMissing_NotAGenericError()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var promo = await (await admin.PostAsJsonAsync("/api/admin/promo-codes", new { code = "SETUP" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), discountPercent = 10, isActive = true }, Json))
            .ReadAsync<AdminPromoCodeDto>();

        // The state of a database that was created before the migration AddEmailCampaignsAndAdminAlerts.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE EmailCampaignRecipients; DROP TABLE EmailCampaigns;");
        }

        foreach (var url in new[] { "/api/admin/campaigns", $"/api/admin/promo-codes/{promo.Id}/campaign-preview?language=az" })
        {
            var response = await admin.GetAsync(url);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var body = await response.ReadJsonAsync();
            Assert.Equal("campaign_setup_required", body.GetProperty("code").GetString());
            Assert.Contains("migration", body.GetProperty("detail").GetString());
            // Nothing from the exception leaks into the answer.
            Assert.DoesNotContain("EmailCampaigns", body.GetRawText().Replace("AddEmailCampaignsAndAdminAlerts", string.Empty));
            Assert.DoesNotContain("SqlException", body.GetRawText());
        }

        var send = await admin.PostAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/campaign", new { previewHash = "x", confirm = true }, Json);
        // Without a preview there is no valid hash, so nothing can be sent (and nothing is written).
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);

        // The rest of the admin page and the ordinary flows are not affected.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/promo-codes")).StatusCode);
    }
}

public class CampaignHistoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CampaignHistoryTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CampaignHistory_IsAnEmptyList_WhenThereAreNoCampaigns()
    {
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.GetAsync("/api/admin/campaigns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsync<List<CampaignDto>>());
    }

    [Fact]
    public async Task ThePreview_UsesTheRealValuesOfTheSelectedPromoCode()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var code = "REAL" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var validTo = DateTime.UtcNow.AddDays(12);
        var created = await (await admin.PostAsJsonAsync("/api/admin/promo-codes", new { code, discountPercent = 35, validTo, maxUses = 7, isActive = true }, Json))
            .ReadAsync<AdminPromoCodeDto>();
        // A different promo code must not leak into this preview.
        await admin.PostAsJsonAsync("/api/admin/promo-codes", new { code = "OTHER" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), discountPercent = 5, isActive = true }, Json);

        var preview = await (await admin.GetAsync($"/api/admin/promo-codes/{created.Id}/campaign-preview?language=en")).ReadAsync<CampaignPreviewDto>();

        Assert.Equal(code, preview.Code);
        Assert.Equal(35, preview.DiscountPercent);
        Assert.Equal(7, preview.MaxUses);
        Assert.Contains(code, preview.Text);
        Assert.Contains("35%", preview.Subject);
        Assert.Contains($"Valid until {validTo:yyyy-MM-dd}", preview.Text);
        Assert.Contains("Limited number of uses: 7", preview.Text);
        Assert.Equal(0, preview.RecipientCount);
        Assert.False(preview.CanSend);
        Assert.Contains("no_subscribers", preview.Problems);
    }
}
