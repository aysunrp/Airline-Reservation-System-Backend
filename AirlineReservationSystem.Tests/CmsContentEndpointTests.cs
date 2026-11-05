using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The FAQ items and the home cards managed in the admin panel: CRUD, the card image upload, the public lists and the seed.</summary>
public class CmsContentEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CmsContentEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = false;
    }

    private static string FileOnDisk(IServiceScope scope, string relativePath)
    {
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        return Path.Combine(env.ContentRootPath, "Media", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static FaqItemRequestDto Faq(string question = "Test sualı?", bool active = true)
        => new() { Question = question, Answer = "Test cavabı.", IsActive = active };

    private static HomeCardRequestDto Card(string section = "premium", string title = "Test kartı")
        => new() { Section = section, Title = title, Description = "Test təsviri.", LinkText = "Keç", LinkUrl = "booking.html", IsActive = true };

    [Fact]
    public async Task Faq_Admin_CreateEditDelete_AndPublicShowsOnlyActive()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

        var created = await (await admin.PostAsJsonAsync("/api/admin/faq", Faq("Sual bir?"), Json)).ReadAsync<FaqItemDto>();
        Assert.True(created.IsActive);
        Assert.True(created.SortOrder >= 1);

        var detail = await (await admin.GetAsync($"/api/admin/faq/{created.Id}")).ReadAsync<FaqItemDto>();
        Assert.Equal("Sual bir?", detail.Question);

        var publicList = await (await _factory.CreateClient().GetAsync("/api/faq")).ReadAsync<List<FaqItemDto>>();
        Assert.Contains(publicList, i => i.Id == created.Id);

        var edit = Faq("Sual bir, dəyişdi?", active: false);
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/faq/{created.Id}", edit, Json)).ReadAsync<FaqItemDto>();
        Assert.Equal("Sual bir, dəyişdi?", updated.Question);
        Assert.False(updated.IsActive);

        var hidden = await (await _factory.CreateClient().GetAsync("/api/faq")).ReadAsync<List<FaqItemDto>>();
        Assert.DoesNotContain(hidden, i => i.Id == created.Id);
        var adminList = await (await admin.GetAsync("/api/admin/faq")).ReadAsync<List<FaqItemDto>>();
        Assert.Contains(adminList, i => i.Id == created.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/faq/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/faq/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/faq/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Faq_RejectsEmptyOrTooLongText_AndRequiresAdmin()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var customer = (await _factory.CreateAccountAsync(UserRole.User)).Client;

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/faq", new FaqItemRequestDto { Question = " ", Answer = "x" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/faq", new FaqItemRequestDto { Question = "x", Answer = new string('a', 3001) }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/admin/faq", Faq(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/admin/faq", Faq(), Json)).StatusCode);
    }

    [Fact]
    public async Task HomeCards_Admin_CreateEditDelete_AndPublicSectionFilter()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

        var created = await (await admin.PostAsJsonAsync("/api/admin/home-cards", Card("why", "Üstünlük"), Json)).ReadAsync<HomeCardDto>();
        Assert.Equal("why", created.Section);
        Assert.Null(created.ImagePath);

        var why = await (await _factory.CreateClient().GetAsync("/api/home-cards?section=why")).ReadAsync<List<HomeCardDto>>();
        Assert.Contains(why, c => c.Id == created.Id);
        var premium = await (await _factory.CreateClient().GetAsync("/api/home-cards?section=premium")).ReadAsync<List<HomeCardDto>>();
        Assert.DoesNotContain(premium, c => c.Id == created.Id);

        var edit = Card("why", "Üstünlük dəyişdi");
        edit.IsActive = false;
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/home-cards/{created.Id}", edit, Json)).ReadAsync<HomeCardDto>();
        Assert.Equal("Üstünlük dəyişdi", updated.Title);
        Assert.False(updated.IsActive);

        var hidden = await (await _factory.CreateClient().GetAsync("/api/home-cards?section=why")).ReadAsync<List<HomeCardDto>>();
        Assert.DoesNotContain(hidden, c => c.Id == created.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/home-cards/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/home-cards/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("other", "booking.html", null)]
    [InlineData("why", "javascript:alert(1)", null)]
    [InlineData("why", "http://example.com", null)]
    [InlineData("why", "booking.html", "unknown-icon")]
    public async Task HomeCards_RejectBadSectionLinkOrIcon(string section, string link, string? icon)
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var request = Card(section);
        request.LinkUrl = link;
        request.IconKey = icon;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/home-cards", request, Json)).StatusCode);
    }

    [Fact]
    public async Task HomeCards_ImageUpload_Replace_Remove_AndDeleteCleanUpFiles()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var card = await (await admin.PostAsJsonAsync("/api/admin/home-cards", Card(), Json)).ReadAsync<HomeCardDto>();

        var first = await (await admin.PostImageFileAsync($"/api/admin/home-cards/{card.Id}/image")).ReadAsync<CardImageDto>();
        Assert.StartsWith("home/", first.ImagePath, StringComparison.Ordinal);
        Assert.StartsWith("/media/home/", first.ImageUrl, StringComparison.Ordinal);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True(File.Exists(FileOnDisk(scope, first.ImagePath)));
        }

        var shown = await (await _factory.CreateClient().GetAsync("/api/home-cards?section=premium")).ReadAsync<List<HomeCardDto>>();
        Assert.Equal(first.ImagePath, shown.Single(c => c.Id == card.Id).ImagePath);

        var second = await (await admin.PostImageFileAsync($"/api/admin/home-cards/{card.Id}/image")).ReadAsync<CardImageDto>();
        Assert.NotEqual(first.ImagePath, second.ImagePath);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(FileOnDisk(scope, first.ImagePath)));
            Assert.True(File.Exists(FileOnDisk(scope, second.ImagePath)));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/home-cards/{card.Id}/image")).StatusCode);
        var cleared = await (await admin.GetAsync($"/api/admin/home-cards/{card.Id}")).ReadAsync<HomeCardDto>();
        Assert.Null(cleared.ImagePath);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(FileOnDisk(scope, second.ImagePath)));
        }

        var third = await (await admin.PostImageFileAsync($"/api/admin/home-cards/{card.Id}/image")).ReadAsync<CardImageDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/home-cards/{card.Id}")).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(FileOnDisk(scope, third.ImagePath)));
        }
    }

    [Fact]
    public async Task HomeCards_ImageUpload_RejectsNonImageAndNeedsAdmin()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var customer = (await _factory.CreateAccountAsync(UserRole.User)).Client;
        var card = await (await admin.PostAsJsonAsync("/api/admin/home-cards", Card(), Json)).ReadAsync<HomeCardDto>();
        var url = $"/api/admin/home-cards/{card.Id}/image";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostImageFileAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostImageFileAsync(url)).StatusCode);

        var notAnImage = await admin.PostImageFileAsync(url, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, "file.png");
        Assert.Equal(HttpStatusCode.BadRequest, notAnImage.StatusCode);
        var wrongExtension = await admin.PostImageFileAsync(url, fileName: "script.exe");
        Assert.Equal(HttpStatusCode.BadRequest, wrongExtension.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostImageFileAsync("/api/admin/home-cards/999999/image")).StatusCode);
    }

    [Fact]
    public async Task Seed_AddsTheCurrentCardsAndQuestionsOnce_AndNeverAgainAfterAdminDeletesThem()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ICmsListSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.FaqItems.RemoveRange(db.FaqItems);
        db.HomeCards.RemoveRange(db.HomeCards);
        db.SiteContents.RemoveRange(db.SiteContents.Where(c => c.Page == "system"));
        await db.SaveChangesAsync();

        var added = await seeder.SeedAsync();
        Assert.Equal(8 + 9, added);
        Assert.Equal(8, await db.FaqItems.CountAsync());
        Assert.Equal(6, await db.HomeCards.CountAsync(c => c.Section == "why"));
        Assert.Equal(3, await db.HomeCards.CountAsync(c => c.Section == "premium"));
        Assert.True(await db.HomeCards.AnyAsync(c => c.ImagePath != null && c.ImagePath.StartsWith("assets/images/")));

        Assert.Equal(0, await seeder.SeedAsync());

        db.FaqItems.RemoveRange(db.FaqItems);
        await db.SaveChangesAsync();
        Assert.Equal(0, await seeder.SeedAsync());
        Assert.Equal(0, await db.FaqItems.CountAsync());
    }

    [Fact]
    public async Task SitePage_AdminAddsContactDetails_AndDestinationsHeadingsArePublic()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var key = "contact.email";

        var created = await admin.PostAsJsonAsync("/api/admin/site-content", new { page = "site", key, textAz = "info@aerova.az" }, Json);
        Assert.True(created.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict);

        var site = await (await _factory.CreateClient().GetAsync("/api/site-content/site")).ReadAsync<List<Service.Helpers.DTOs.SiteContent.SiteContentDto>>();
        Assert.Contains(site, c => c.Key == key);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/site-content/{site.Single(c => c.Key == key).Id}")).StatusCode);

        Assert.True(Service.Helpers.Seeding.SiteContentData.IsManagedCmsPage("destinations"));
        Assert.Contains(Service.Helpers.Seeding.SiteContentData.Destinations, c => c.Key == "destinations.allDestinationsTitle");
    }
}
