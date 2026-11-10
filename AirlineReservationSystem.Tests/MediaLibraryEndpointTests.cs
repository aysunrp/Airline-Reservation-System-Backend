using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Cms;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The media library: listing with usage, replacing an image in place, deleting only unused files, and the fixed site image slots.</summary>
public class MediaLibraryEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MediaLibraryEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = false;
    }

    private string FileOnDisk(string relativePath)
    {
        using var scope = _factory.Services.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        return Path.Combine(env.ContentRootPath, "Media", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static Task<HttpResponseMessage> PutImageAsync(HttpClient client, string url, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyPng);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Image.Png);
        form.Add(file, "file", fileName);
        return client.PutAsync(url, form);
    }

    [Fact]
    public async Task Slots_Upload_Replace_Clear_AndTheFileIsCleanedUp()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

        var slots = await (await admin.GetAsync("/api/admin/media/slots")).ReadAsync<List<SiteImageSlotDto>>();
        Assert.Contains(slots, s => s.Slot == "logo" && s.Path == null);

        var first = await (await admin.PostImageFileAsync("/api/admin/media/slots/about.hero")).ReadAsync<SiteImageSlotDto>();
        Assert.StartsWith("site/", first.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(FileOnDisk(first.Path!)));

        var publicSite = await (await _factory.CreateClient().GetAsync("/api/site-content/site")).ReadAsync<List<Service.Helpers.DTOs.SiteContent.SiteContentDto>>();
        Assert.Contains(publicSite, c => c.Key == "image.about.hero" && c.TextAz == first.Path);

        var second = await (await admin.PostImageFileAsync("/api/admin/media/slots/about.hero")).ReadAsync<SiteImageSlotDto>();
        Assert.NotEqual(first.Path, second.Path);
        Assert.False(File.Exists(FileOnDisk(first.Path!)));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/admin/media/slots/about.hero")).StatusCode);
        Assert.False(File.Exists(FileOnDisk(second.Path!)));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostImageFileAsync("/api/admin/media/slots/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostImageFileAsync("/api/admin/media/slots/logo", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })).StatusCode);
    }

    [Fact]
    public async Task Library_ShowsUsage_ReplacesInPlace_AndRefusesToDeleteUsedFiles()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var slot = await (await admin.PostImageFileAsync("/api/admin/media/slots/logo")).ReadAsync<SiteImageSlotDto>();

        var list = await (await admin.GetAsync("/api/admin/media?category=site")).ReadAsync<List<MediaFileDto>>();
        var entry = list.Single(f => f.Path == slot.Path);
        Assert.Contains(entry.UsedBy, u => u.Contains("image.logo"));
        Assert.True(entry.IsImage);

        var replaced = await (await PutImageAsync(admin, $"/api/admin/media/file?path={slot.Path}", "new.png")).ReadAsync<MediaFileDto>();
        Assert.Equal(slot.Path, replaced.Path);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutImageAsync(admin, $"/api/admin/media/file?path={slot.Path}", "new.jpg")).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/media/file?path={slot.Path}")).StatusCode);
        Assert.True(File.Exists(FileOnDisk(slot.Path!)));

        // A file nothing shows can be deleted; an escaping or unknown path is refused.
        var orphanPath = "site/orphan-" + Guid.NewGuid().ToString("N") + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(FileOnDisk(orphanPath))!);
        await File.WriteAllBytesAsync(FileOnDisk(orphanPath), TinyPng);
        var unused = await (await admin.GetAsync("/api/admin/media?category=site&unusedOnly=true")).ReadAsync<List<MediaFileDto>>();
        Assert.Contains(unused, f => f.Path == orphanPath);
        Assert.DoesNotContain(unused, f => f.Path == slot.Path);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/media/file?path={orphanPath}")).StatusCode);
        Assert.False(File.Exists(FileOnDisk(orphanPath)));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/admin/media/file?path=../appsettings.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/media/file?path=site/missing.png")).StatusCode);

        await admin.DeleteAsync("/api/admin/media/slots/logo");
    }

    [Fact]
    public async Task Library_NeedsAnAdmin()
    {
        var customer = (await _factory.CreateAccountAsync(UserRole.User)).Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/media")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/media")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostImageFileAsync("/api/admin/media/slots/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync("/api/admin/media/file?path=site/x.png")).StatusCode);
    }
}
