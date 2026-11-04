using Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Service.Helpers;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

public class SightMediaServiceTests
{
    private sealed class StubMedia : ISiteMediaStorage
    {
        public string SavedRelative { get; set; } = "sights/img-test.webp";

        public Task<string> SaveImageAsync(string category, IFormFile file, long? maxBytes = null, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedRelative);

        public Task<bool> TryDeleteRelativeFileAsync(string? relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string ToPublicUrl(string relativePath) => "/media/" + relativePath;

        public string ToRelativePath(string publicOrRelativePath) => publicOrRelativePath.TrimStart('/').Replace("media/", "");

        public Task<string> SaveHeroVideoAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<string> SaveHeroPosterAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public string NormalizeHeroMediaUrl(string? url, bool required, bool allowVideoExtensions) => url ?? string.Empty;
    }

    private static IFormFile TinyFile()
        => new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "file", "x.png");

    private static SightMediaService CreateService(FakeSiteContentRepository repo, StubMedia media)
    {
        var references = new SiteMediaReferenceService(new FakeDestinationCatalogForMedia(), repo, media);
        return new SightMediaService(repo, media, references);
    }

    [Fact]
    public async Task Upload_StoresRelativePathUnderSights()
    {
        var repo = new FakeSiteContentRepository();
        var service = CreateService(repo, new StubMedia());

        var result = await service.UploadAsync("paris", 0, TinyFile());

        Assert.Equal("sights/img-test.webp", result.RelativePath);
        var stored = Assert.Single(repo.All);
        Assert.Equal("sight-media", stored.Page);
        Assert.Equal("paris.0", stored.Key);
        Assert.Equal("sights/img-test.webp", stored.TextAz);
    }

    [Fact]
    public async Task Delete_RemovesSiteContentRow()
    {
        var repo = new FakeSiteContentRepository(new SiteContent
        {
            Id = 1,
            Page = SightMediaService.Page,
            Key = "paris.0",
            TextAz = "sights/old.webp"
        });

        await CreateService(repo, new StubMedia()).DeleteAsync("paris", 0);

        Assert.Empty(repo.All);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("bad slug!", 0)]
    [InlineData("paris", 25)]
    public async Task InvalidSlugOrIndex_IsRejected(string slug, int index)
    {
        var service = CreateService(new FakeSiteContentRepository(), new StubMedia());

        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(slug, index, TinyFile()));
    }
}
