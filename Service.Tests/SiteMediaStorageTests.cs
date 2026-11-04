using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

public class SiteMediaStorageTests
{
    private static SiteMediaStorage Create(string contentRoot)
    {
        var env = new TestHostEnvironment(contentRoot);
        return new SiteMediaStorage(
            Options.Create(new SiteMediaSettings()),
            Options.Create(new HeroMediaSettings()),
            env);
    }

    [Fact]
    public void ToPublicUrl_PrefixesRelativePath()
    {
        var storage = Create(Path.GetTempPath());

        Assert.Equal("/media/destinations/img-abc.webp", storage.ToPublicUrl("destinations/img-abc.webp"));
    }

    [Fact]
    public void ToRelativePath_StripsPublicPrefix()
    {
        var storage = Create(Path.GetTempPath());

        Assert.Equal("sights/paris-0.webp", storage.ToRelativePath("/media/sights/paris-0.webp"));
    }

    [Fact]
    public async Task TryDeleteRelativeFileAsync_RemovesFileUnderMediaRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aerova-media-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, SiteMediaCategories.Destinations));
        var relative = $"{SiteMediaCategories.Destinations}/test-{Guid.NewGuid():N}.txt";
        var full = Path.Combine(root, "Media", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "x");

        var storage = Create(root);
        Assert.True(await storage.TryDeleteRelativeFileAsync(relative));
        Assert.False(File.Exists(full));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Service.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
