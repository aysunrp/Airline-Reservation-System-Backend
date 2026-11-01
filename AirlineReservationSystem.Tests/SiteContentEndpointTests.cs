using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The editable Azerbaijani texts of the AEROVA haqqında page: seed, public API and admin edits.</summary>
public class SiteContentEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SiteContentEndpointTests(ApiFactory factory) => _factory = factory;

    private async Task<SiteContentSeedResultDto> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISiteContentSeeder>().SeedAsync();
    }

    private static string Errors(string json) => JsonDocument.Parse(json).RootElement.GetProperty("errors").GetProperty("TextAz")[0].GetString()!;

    [Fact]
    public async Task RunningTheSeedAgain_AddsNothing_AndNeverOverwritesAnAdminEdit()
    {
        await SeedAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var items = await (await admin.GetAsync("/api/admin/site-content?page=about")).ReadAsync<List<SiteContentDto>>();
        var hero = items.Single(i => i.Key == "heroTitle");
        var edit = await admin.PutAsJsonAsync($"/api/admin/site-content/{hero.Id}", new { textAz = "Redaktə" }, Json);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var again = await SeedAsync();

        Assert.Equal(0, again.Added);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(SiteContentData.About.Count, await db.SiteContents.CountAsync(c => c.Page == "about"));
        Assert.Equal("Redaktə", (await db.SiteContents.SingleAsync(c => c.Page == "about" && c.Key == "heroTitle")).TextAz);
    }

    [Fact]
    public async Task ThePublicPage_HasEveryKey_InAzerbaijanOnly()
    {
        await SeedAsync();

        var response = await _factory.CreateClient().GetAsync("/api/site-content/about");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.ReadAsync<List<SiteContentDto>>();
        Assert.Equal(SiteContentData.About.Count, items.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.TextAz)));
        Assert.Equal("AEROVA-nın hekayəsi", items.Single(i => i.Key == "storyTitle").TextAz);

        // Nothing but the Azerbaijani text is sent: no English or Russian field, in the public or the admin answer.
        var publicJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement[0];
        Assert.Equal(new[] { "id", "key", "page", "textAz" }, publicJson.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var adminJson = JsonDocument.Parse(await (await admin.GetAsync("/api/admin/site-content?page=about")).Content.ReadAsStringAsync()).RootElement[0];
        Assert.Equal(new[] { "id", "key", "page", "textAz" }, adminJson.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ThePublicPage_IsNotFound_ForAPageWithNoContent_InAzerbaijani()
    {
        var response = await _factory.CreateClient().GetAsync("/api/site-content/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Bu səhifənin mətnləri tapılmadı.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OnlyAnAdmin_EditsTheTexts_AndAnEditIsAudited()
    {
        await SeedAsync();
        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var item = (await (await admin.GetAsync("/api/admin/site-content?page=about")).ReadAsync<List<SiteContentDto>>()).First();
        var body = new { textAz = "Yeni mətn" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/site-content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/site-content")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PutAsJsonAsync($"/api/admin/site-content/{item.Id}", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync($"/api/admin/site-content/{item.Id}", body, Json)).StatusCode);
        var untouched = await (await _factory.CreateClient().GetAsync("/api/site-content/about")).ReadAsync<List<SiteContentDto>>();
        Assert.Equal(item.TextAz, untouched.Single(i => i.Id == item.Id).TextAz); // the refused edits changed nothing

        var ok = await admin.PutAsJsonAsync($"/api/admin/site-content/{item.Id}", body, Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(new SiteContentDto(item.Id, item.Page, item.Key, "Yeni mətn"), await ok.ReadAsync<SiteContentDto>());
        var publicItems = await (await _factory.CreateClient().GetAsync("/api/site-content/about")).ReadAsync<List<SiteContentDto>>();
        Assert.Equal("Yeni mətn", publicItems.Single(i => i.Id == item.Id).TextAz); // the public page shows the edit

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "SiteContentUpdated" && a.TargetId == item.Id.ToString() && a.Details == $"{item.Page}/{item.Key}"));
    }

    [Fact]
    public async Task AnEmptyOrTooLongText_IsRefused_InAzerbaijani_AndNothingChanges()
    {
        await SeedAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var item = (await (await admin.GetAsync("/api/admin/site-content?page=about")).ReadAsync<List<SiteContentDto>>()).First();
        Task<HttpResponseMessage> Put(object body) => admin.PutAsJsonAsync($"/api/admin/site-content/{item.Id}", body, Json);

        var empty = await Put(new { textAz = "" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("Mətn boş ola bilməz.", Errors(await empty.Content.ReadAsStringAsync()));

        var blank = await Put(new { textAz = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("Mətn boş ola bilməz.", Errors(await blank.Content.ReadAsStringAsync()));

        var missing = await Put(new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("Mətn boş ola bilməz.", Errors(await missing.Content.ReadAsStringAsync()));

        var nothing = await Put(new { textAz = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        Assert.Equal("Mətn boş ola bilməz.", Errors(await nothing.Content.ReadAsStringAsync()));

        var tooLong = await Put(new { textAz = new string('x', 1001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("Mətn 1000 simvoldan uzun ola bilməz. İndi 1001 simvol var.", Errors(await tooLong.Content.ReadAsStringAsync()));

        // A body that is not a text at all gets the same answer, in Azerbaijani.
        var noBody = await admin.PutAsync($"/api/admin/site-content/{item.Id}", new StringContent(string.Empty));
        Assert.True(noBody.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType);

        Assert.Equal(HttpStatusCode.OK, (await Put(new { textAz = new string('ə', 1000) })).StatusCode); // exactly 1000 is accepted
        var after = await (await _factory.CreateClient().GetAsync("/api/site-content/about")).ReadAsync<List<SiteContentDto>>();
        Assert.Equal(1000, after.Single(i => i.Id == item.Id).TextAz.Length);
    }

    [Fact]
    public async Task AnUnknownText_IsNotFound_InAzerbaijani()
    {
        await SeedAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

        var response = await admin.PutAsJsonAsync("/api/admin/site-content/999999", new { textAz = "Yeni" }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Mətn tapılmadı.", await response.Content.ReadAsStringAsync());
        var list = await admin.GetAsync("/api/admin/site-content?page=nothing-here");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Contains("Bu səhifənin mətnləri tapılmadı.", await list.Content.ReadAsStringAsync());
    }
}
