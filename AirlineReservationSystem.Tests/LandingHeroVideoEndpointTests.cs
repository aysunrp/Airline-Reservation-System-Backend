using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers;
using Service.Helpers.DTOs.LandingHeroVideo;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class LandingHeroVideoEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LandingHeroVideoEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PublicEndpoint_ReturnsNoContent_WhenNothingIsActive()
    {
        var response = await _factory.CreateClient().GetAsync("/api/landing/hero-video");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanPublish_AndPublicSeesActiveVideo()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var create = await admin.PostAsJsonAsync("/api/admin/hero-videos", new
        {
            title = "Sunset cruise",
            videoUrl = "https://cdn.example.com/hero.mp4",
            posterUrl = "https://cdn.example.com/hero-poster.jpg",
            isActive = true,
            sourceName = "Pexels",
            licenseInfo = "Pexels License"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.ReadAsync<LandingHeroVideoDto>();

        var publicResponse = await _factory.CreateClient().GetAsync("/api/landing/hero-video");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var published = await publicResponse.ReadAsync<LandingHeroVideoPublicDto>();
        Assert.Equal("https://cdn.example.com/hero.mp4", published.VideoUrl);

        var deactivate = await admin.PutAsJsonAsync($"/api/admin/hero-videos/{created.Id}", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CreateClient().GetAsync("/api/landing/hero-video")).StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_AreRejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/hero-videos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/admin/hero-videos", new { title = "X", videoUrl = "https://a.test/v.mp4" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Activate_WritesAuditLog()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var first = await (await admin.PostAsJsonAsync("/api/admin/hero-videos", new
        {
            title = "First",
            videoUrl = "https://cdn.example.com/a.mp4",
            isActive = false
        }, Json)).ReadAsync<LandingHeroVideoDto>();
        var second = await (await admin.PostAsJsonAsync("/api/admin/hero-videos", new
        {
            title = "Second",
            videoUrl = "https://cdn.example.com/b.mp4",
            isActive = false
        }, Json)).ReadAsync<LandingHeroVideoDto>();

        await admin.PostAsync($"/api/admin/hero-videos/{second.Id}/activate", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.HeroVideoActivated && a.TargetId == second.Id.ToString()));
        var active = await db.LandingHeroVideos.Where(v => v.IsActive).ToListAsync();
        Assert.Single(active);
        Assert.Equal(second.Id, active[0].Id);
        Assert.False((await db.LandingHeroVideos.SingleAsync(v => v.Id == first.Id)).IsActive);
    }

    [Fact]
    public async Task AdminCanDelete_AnInactiveHeroVideo_ButNotTheActiveOne()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var draft = await (await admin.PostAsJsonAsync("/api/admin/hero-videos", new
        {
            title = "Draft",
            videoUrl = "https://cdn.example.com/draft.mp4",
            isActive = false
        }, Json)).ReadAsync<LandingHeroVideoDto>();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/hero-videos/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/hero-videos/{draft.Id}")).StatusCode);

        var live = await (await admin.PostAsJsonAsync("/api/admin/hero-videos", new
        {
            title = "Live",
            videoUrl = "https://cdn.example.com/live.mp4",
            isActive = true
        }, Json)).ReadAsync<LandingHeroVideoDto>();

        var blocked = await admin.DeleteAsync($"/api/admin/hero-videos/{live.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("Yayımda olan hero video silinə bilməz", await blocked.Content.ReadAsStringAsync());

        await admin.PutAsJsonAsync($"/api/admin/hero-videos/{live.Id}", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/hero-videos/{live.Id}")).StatusCode);
    }
}
