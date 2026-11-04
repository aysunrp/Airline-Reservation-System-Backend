using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class SiteMediaStorage : ISiteMediaStorage
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> PosterExtensions = ImageExtensions;

    private readonly SiteMediaSettings _site;
    private readonly HeroMediaSettings _hero;
    private readonly IHostEnvironment _environment;

    public SiteMediaStorage(IOptions<SiteMediaSettings> site, IOptions<HeroMediaSettings> hero, IHostEnvironment environment)
    {
        _site = site.Value;
        _hero = hero.Value;
        _environment = environment;
    }

    public string ToRelativePath(string publicOrRelativePath)
    {
        if (string.IsNullOrWhiteSpace(publicOrRelativePath))
        {
            return string.Empty;
        }

        var trimmed = publicOrRelativePath.Trim().Replace('\\', '/');
        var prefix = _site.PublicPathPrefix.TrimEnd('/');
        if (trimmed.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..].TrimStart('/');
        }

        if (trimmed.StartsWith('/'))
        {
            throw Fail("path", "Yalnız yüklənmiş media faylları silinə bilər.");
        }

        return trimmed;
    }

    public string ToPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        return $"{_site.PublicPathPrefix.TrimEnd('/')}/{relativePath.Trim().Replace('\\', '/').TrimStart('/')}";
    }

    public async Task<string> SaveHeroVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var relative = await SaveAsync(file, SiteMediaCategories.Hero, VideoExtensions, _hero.MaxVideoBytes, "video", cancellationToken);
        return ToPublicUrl(relative);
    }

    public async Task<string> SaveHeroPosterAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var relative = await SaveAsync(file, SiteMediaCategories.Hero, PosterExtensions, _hero.MaxPosterBytes, "poster", cancellationToken);
        return ToPublicUrl(relative);
    }

    public async Task<string> SaveImageAsync(string category, IFormFile file, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        ValidateCategory(category);
        var limit = maxBytes ?? (category == SiteMediaCategories.Sights ? _site.MaxSightImageBytes : _site.MaxImageBytes);
        // The content of the file is checked as well as its extension: a renamed script is not an image.
        await ImageFileValidator.EnsureImageAsync(file, cancellationToken);
        return await SaveAsync(file, category, ImageExtensions, limit, "img", cancellationToken);
    }

    public string NormalizeHeroMediaUrl(string? url, bool required, bool allowVideoExtensions)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            if (required)
            {
                throw Fail("url", "Video URL tələb olunur.");
            }

            return string.Empty;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            throw Fail("url", "URL təhlükəsiz deyil.");
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme is not "https" and not "http")
            {
                throw Fail("url", "Yalnız http və ya https URL-lərinə icazə verilir.");
            }

            var ext = Path.GetExtension(absolute.AbsolutePath);
            var allowed = allowVideoExtensions ? VideoExtensions : PosterExtensions;
            if (!string.IsNullOrEmpty(ext) && !allowed.Contains(ext))
            {
                throw Fail("url", allowVideoExtensions ? "Video URL dəstəklənən formatda olmalıdır (.mp4, .webm)." : "Poster URL dəstəklənən formatda olmalıdır.");
            }

            return absolute.ToString();
        }

        if (!trimmed.StartsWith('/'))
        {
            throw Fail("url", "Nisbi URL / ilə başlamalıdır.");
        }

        var heroPrefix = _hero.PublicPath.TrimEnd('/');
        if (!trimmed.StartsWith(heroPrefix + "/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(trimmed, heroPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw Fail("url", $"Yalnız {heroPrefix}/ altındakı fayl yollarına icazə verilir.");
        }

        var extension = Path.GetExtension(trimmed);
        var allowedExt = allowVideoExtensions ? VideoExtensions : PosterExtensions;
        if (string.IsNullOrEmpty(extension) || !allowedExt.Contains(extension))
        {
            throw Fail("url", "Fayl növü dəstəklənmir.");
        }

        return trimmed;
    }

    public async Task<bool> TryDeleteRelativeFileAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var relative = ToRelativePath(relativePath).Replace('\\', '/');
        if (relative.Contains("..", StringComparison.Ordinal) || relative.StartsWith('/'))
        {
            throw Fail("path", "Fayl yolu etibarsızdır.");
        }

        ValidateCategory(relative.Split('/')[0]);
        var fullPath = Path.GetFullPath(Path.Combine(MediaRoot, relative));
        var rootFull = Path.GetFullPath(MediaRoot);
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return false;
        }

        await Task.Run(() => File.Delete(fullPath), cancellationToken);
        return true;
    }

    private string MediaRoot => Path.Combine(_environment.ContentRootPath, _site.RootPath);

    private async Task<string> SaveAsync(IFormFile file, string category, HashSet<string> extensions, long maxBytes, string prefix, CancellationToken cancellationToken)
    {
        ValidateCategory(category);
        if (file.Length <= 0)
        {
            throw Fail("file", "Boş fayl yüklənə bilməz.");
        }

        if (file.Length > maxBytes)
        {
            throw Fail("file", "Fayl çox böyükdür.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension))
        {
            throw Fail("file", "Fayl növü dəstəklənmir.");
        }

        var folder = Path.Combine(MediaRoot, category);
        Directory.CreateDirectory(folder);
        var safeName = $"{prefix}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, safeName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"{category}/{safeName}";
    }

    private static void ValidateCategory(string category)
    {
        if (category is not SiteMediaCategories.Hero and not SiteMediaCategories.Destinations and not SiteMediaCategories.Sights and not SiteMediaCategories.Home and not SiteMediaCategories.Site)
        {
            throw Fail("category", "Media kateqoriyası dəstəklənmir.");
        }
    }

    private static ValidationException Fail(string field, string message)
        => new(new[] { new ValidationFailure(field, message) });
}
