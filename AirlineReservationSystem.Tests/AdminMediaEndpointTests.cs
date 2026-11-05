using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.DTOs.Media;
using Service.Helpers.DTOs.SiteContent;
using Service.Services;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>FAZ 2 media upload: destination city/country images and sight-media overrides.</summary>
public class AdminMediaEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminMediaEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = false;
    }

    private async Task SeedDestinationsAsync()
    {
        await _factory.GetSeedAsync();
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();
    }

    private static string MediaFileOnDisk(IServiceScope scope, string relativePath)
    {
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        return Path.Combine(env.ContentRootPath, "Media", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public async Task AdminCanUploadAndRemove_CityImage_StoredAsRelativePath()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var catalog = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var paris = catalog.Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Paris");
        var before = paris.ImageFile;

        var upload = await admin.PostImageFileAsync($"/api/admin/destinations/cities/{paris.Id}/image");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var media = await upload.ReadAsync<MediaUploadResultDto>();
        Assert.StartsWith("destinations/", media.RelativePath, StringComparison.Ordinal);
        Assert.StartsWith("/media/destinations/", media.PublicUrl, StringComparison.Ordinal);

        var publicCatalog = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var updated = publicCatalog.Single(c => c.Code == "FR").Cities.Single(c => c.Id == paris.Id);
        Assert.Equal(media.RelativePath, updated.ImageFile);
        Assert.NotEqual(before, updated.ImageFile);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }

        var remove = await admin.DeleteAsync($"/api/admin/destinations/cities/{paris.Id}/image");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var cleared = (await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR").Cities.Single(c => c.Id == paris.Id);
        Assert.True(string.IsNullOrEmpty(cleared.ImageFile));

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(a =>
                a.Action == AuditActions.DestinationImageUpdated && a.TargetId == paris.Id.ToString()));
        }
    }

    [Fact]
    public async Task AdminCanUploadAndRemove_CountryImage()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var france = (await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR");

        var upload = await admin.PostImageFileAsync($"/api/admin/destinations/countries/{france.Id}/image");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var media = await upload.ReadAsync<MediaUploadResultDto>();
        Assert.StartsWith("destinations/", media.RelativePath, StringComparison.Ordinal);

        var adminList = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.Equal(media.RelativePath, adminList.Single(c => c.Id == france.Id).ImageFile);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/destinations/countries/{france.Id}/image")).StatusCode);
        adminList = await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.True(string.IsNullOrEmpty(adminList.Single(c => c.Id == france.Id).ImageFile));
    }

    [Fact]
    public async Task AnonymousAndCustomer_CannotUploadDestinationImages()
    {
        await SeedDestinationsAsync();
        var catalog = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var cityId = catalog.Single(c => c.Code == "FR").Cities[0].Id;
        var url = $"/api/admin/destinations/cities/{cityId}/image";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostImageFileAsync(url)).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostImageFileAsync(url)).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndCustomer_CannotDeleteCityDestinationImage()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var paris = (await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Paris");
        var upload = await admin.PostImageFileAsync($"/api/admin/destinations/cities/{paris.Id}/image");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var url = $"/api/admin/destinations/cities/{paris.Id}/image";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().DeleteAsync(url)).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync(url)).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndCustomer_CannotDeleteCountryImage()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var france = (await (await admin.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostImageFileAsync($"/api/admin/destinations/countries/{france.Id}/image")).StatusCode);
        var url = $"/api/admin/destinations/countries/{france.Id}/image";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().DeleteAsync(url)).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync(url)).StatusCode);
    }

    [Fact]
    public async Task SharedRelativePath_BetweenTwoDestinationRecords_KeepsFileUntilLastReferenceRemoved()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var catalog = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        var france = catalog.Single(c => c.Code == "FR");
        var germany = catalog.Single(c => c.Code == "DE");

        var media = await (await admin.PostImageFileAsync($"/api/admin/destinations/countries/{france.Id}/image")).ReadAsync<MediaUploadResultDto>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deCountry = await db.Countries.SingleAsync(c => c.Id == germany.Id);
            deCountry.ImageFile = media.RelativePath;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/destinations/countries/{france.Id}/image")).StatusCode);

        var afterDelete = await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.True(string.IsNullOrEmpty(afterDelete.Single(c => c.Code == "FR").ImageFile));
        Assert.Equal(media.RelativePath, afterDelete.Single(c => c.Code == "DE").ImageFile);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/destinations/countries/{germany.Id}/image")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }
    }

    [Fact]
    public async Task AnonymousAndCustomer_CannotDeleteSightMedia()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostImageFileAsync("/api/admin/sight-media/upload?citySlug=paris&sightIndex=0")).StatusCode);
        const string url = "/api/admin/sight-media?citySlug=paris&sightIndex=0";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().DeleteAsync(url)).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync(url)).StatusCode);
    }

    [Fact]
    public async Task UploadedCityImage_IsServedOverHttpAtMediaPath()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var paris = (await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Paris");

        var media = await (await admin.PostImageFileAsync($"/api/admin/destinations/cities/{paris.Id}/image")).ReadAsync<MediaUploadResultDto>();

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }

        var response = await _factory.CreateClient().GetAsync("/media/" + media.RelativePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task SharedRelativePath_IsNotDeletedFromDiskUntilAllReferencesAreGone()
    {
        await SeedDestinationsAsync();
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var paris = (await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>())
            .Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Paris");
        var media = await (await admin.PostImageFileAsync($"/api/admin/destinations/cities/{paris.Id}/image")).ReadAsync<MediaUploadResultDto>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SiteContents.Add(new SiteContent
            {
                Page = SightMediaService.Page,
                Key = "paris.1",
                TextAz = media.RelativePath
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/destinations/cities/{paris.Id}/image")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.True(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/admin/sight-media?citySlug=paris&sightIndex=1")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }
    }

    [Fact]
    public async Task SightMediaOverride_IsPublicOnSiteContent_AndCanBeRemoved()
    {
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var upload = await admin.PostImageFileAsync("/api/admin/sight-media/upload?citySlug=paris&sightIndex=0");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var media = await upload.ReadAsync<MediaUploadResultDto>();
        Assert.StartsWith("sights/", media.RelativePath, StringComparison.Ordinal);

        var publicItems = await (await _factory.CreateClient().GetAsync("/api/site-content/sight-media")).ReadAsync<List<SiteContentDto>>();
        var row = Assert.Single(publicItems, i => i.Key == "paris.0");
        Assert.Equal(media.RelativePath, row.TextAz);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/admin/sight-media?citySlug=paris&sightIndex=0")).StatusCode);
        Assert.Empty(await (await admin.GetAsync("/api/admin/sight-media")).ReadAsync<List<SiteContentDto>>());
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/site-content/sight-media")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.False(File.Exists(MediaFileOnDisk(scope, media.RelativePath)));
        }
    }
}
