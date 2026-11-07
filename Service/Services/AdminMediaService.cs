using Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// The media library: the files under Media/, where each one is used, replacing an image in place, deleting a file nothing uses, and the
/// pictures of the site that have a fixed place on a page (stored as "image.{slot}" rows of the site page, so no table of their own is needed).
/// </summary>
public class AdminMediaService : IAdminMediaService
{
    private const string SitePage = "site";
    private const string SlotKeyPrefix = "image.";
    private static readonly string[] Categories = { SiteMediaCategories.Hero, SiteMediaCategories.Destinations, SiteMediaCategories.Home, SiteMediaCategories.Site };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static readonly IReadOnlyDictionary<string, string> Slots = new Dictionary<string, string>
    {
        ["logo"] = "Loqo (başlıq və footer)",
        ["about.banner"] = "Haqqında: yuxarıdakı böyük fon şəkli",
        ["about.hero"] = "Haqqında: hekayə bölməsinin şəkli",
        ["about.economy"] = "Haqqında: Economy salonu",
        ["about.comfort"] = "Haqqında: Comfort salonu",
        ["about.business"] = "Haqqında: Business salonu",
        ["about.journey"] = "Haqqında: səyahət addımlarının fon şəkli",
        ["about.final"] = "Haqqında: aşağıdakı yekun fon şəkli",
    };

    private readonly IMediaUsageRepository _usage;
    private readonly ISiteContentRepository _contents;
    private readonly ISiteMediaStorage _media;
    private readonly SiteMediaSettings _settings;
    private readonly IHostEnvironment _environment;

    public AdminMediaService(IMediaUsageRepository usage, ISiteContentRepository contents, ISiteMediaStorage media, IOptions<SiteMediaSettings> settings, IHostEnvironment environment)
    {
        _usage = usage;
        _contents = contents;
        _media = media;
        _settings = settings.Value;
        _environment = environment;
    }

    private string MediaRoot => Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _settings.RootPath));

    public async Task<IReadOnlyList<MediaFileDto>> ListAsync(string? category, bool unusedOnly, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(category) && !Categories.Contains(category))
        {
            throw Fail("category", "Media kateqoriyası dəstəklənmir.");
        }

        var usage = await UsageAsync(cancellationToken);
        var files = new List<MediaFileDto>();
        foreach (var name in Categories.Where(c => string.IsNullOrWhiteSpace(category) || c == category))
        {
            var folder = Path.Combine(MediaRoot, name);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var info in new DirectoryInfo(folder).EnumerateFiles())
            {
                var relative = $"{name}/{info.Name}";
                var usedBy = usage.TryGetValue(relative, out var labels) ? labels.Distinct().ToList() : new List<string>();
                if (unusedOnly && usedBy.Count > 0)
                {
                    continue;
                }

                files.Add(new MediaFileDto(relative, name, info.Name, _media.ToPublicUrl(relative), info.Length, info.LastWriteTimeUtc, ImageExtensions.Contains(info.Extension), usedBy));
            }
        }

        return files.OrderByDescending(f => f.ModifiedUtc).ToList();
    }

    public async Task<MediaFileDto> ReplaceAsync(string path, IFormFile file, CancellationToken cancellationToken = default)
    {
        var (relative, fullPath) = ResolveExisting(path);
        var extension = Path.GetExtension(relative);
        if (!ImageExtensions.Contains(extension))
        {
            throw Fail("path", "Yalnız şəkil faylı dəyişdirilə bilər.");
        }

        if (file.Length <= 0 || file.Length > _settings.MaxImageBytes)
        {
            throw Fail("file", "Fayl boş və ya çox böyükdür.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw Fail("file", $"Yeni fayl eyni növdə ({extension}) olmalıdır.");
        }

        await ImageFileValidator.EnsureImageAsync(file, cancellationToken);

        // Written next to the old file first, so a broken upload never leaves half a picture behind.
        var temp = fullPath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        File.Move(temp, fullPath, overwrite: true);
        var usage = await UsageAsync(cancellationToken);
        var info = new FileInfo(fullPath);
        return new MediaFileDto(relative, relative.Split('/')[0], info.Name, _media.ToPublicUrl(relative), info.Length, info.LastWriteTimeUtc, true,
            usage.TryGetValue(relative, out var labels) ? labels.Distinct().ToList() : new List<string>());
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var (relative, _) = ResolveExisting(path);
        var usage = await UsageAsync(cancellationToken);
        if (usage.TryGetValue(relative, out var labels) && labels.Count > 0)
        {
            throw new ConflictException("Fayl istifadə olunur və silinə bilməz: " + string.Join("; ", labels.Distinct()));
        }

        await _media.TryDeleteRelativeFileAsync(relative, cancellationToken);
    }

    public async Task<IReadOnlyList<SiteImageSlotDto>> GetSlotsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _contents.GetByPageAsync(SitePage, cancellationToken);
        return Slots.Select(slot =>
        {
            var path = rows.FirstOrDefault(r => r.Key == SlotKeyPrefix + slot.Key)?.TextAz;
            return new SiteImageSlotDto(slot.Key, slot.Value, string.IsNullOrWhiteSpace(path) ? null : path, string.IsNullOrWhiteSpace(path) ? null : _media.ToPublicUrl(path));
        }).ToList();
    }

    public async Task<SiteImageSlotDto> SetSlotAsync(string slot, IFormFile file, CancellationToken cancellationToken = default)
    {
        var label = SlotLabel(slot);
        var relative = await _media.SaveImageAsync(SiteMediaCategories.Site, file, cancellationToken: cancellationToken);
        var key = SlotKeyPrefix + slot;
        var row = await _contents.FindTrackedAsync(SitePage, key, cancellationToken);
        var previous = row?.TextAz;
        if (row is null)
        {
            await _contents.AddAsync(new SiteContent { Page = SitePage, Key = key, TextAz = relative }, cancellationToken);
        }
        else
        {
            row.TextAz = relative;
            _contents.Update(row);
        }

        await _contents.SaveChangesAsync(cancellationToken);
        await DeleteIfUnusedAsync(previous, cancellationToken);
        return new SiteImageSlotDto(slot, label, relative, _media.ToPublicUrl(relative));
    }

    public async Task ClearSlotAsync(string slot, CancellationToken cancellationToken = default)
    {
        SlotLabel(slot);
        var row = await _contents.FindTrackedAsync(SitePage, SlotKeyPrefix + slot, cancellationToken);
        if (row is null)
        {
            return;
        }

        var previous = row.TextAz;
        _contents.Delete(row);
        await _contents.SaveChangesAsync(cancellationToken);
        await DeleteIfUnusedAsync(previous, cancellationToken);
    }

    private async Task DeleteIfUnusedAsync(string? path, CancellationToken cancellationToken)
    {
        var relative = Normalize(path);
        if (!relative.StartsWith(SiteMediaCategories.Site + "/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var usage = await UsageAsync(cancellationToken);
        if (!usage.ContainsKey(relative))
        {
            await _media.TryDeleteRelativeFileAsync(relative, cancellationToken);
        }
    }

    private async Task<Dictionary<string, List<string>>> UsageAsync(CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in await _usage.GetReferencesAsync(cancellationToken))
        {
            var key = Normalize(reference.Path);
            if (key.Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = new List<string>();
            }

            list.Add(reference.Label);
        }

        return map;
    }

    private string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var text = path.Trim().Replace('\\', '/');
        var prefix = _settings.PublicPathPrefix.TrimEnd('/') + "/";
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            text = text[prefix.Length..];
        }

        return text.TrimStart('/');
    }

    private (string Relative, string FullPath) ResolveExisting(string path)
    {
        var relative = Normalize(path);
        var parts = relative.Split('/');
        if (parts.Length != 2 || relative.Contains("..", StringComparison.Ordinal) || !Categories.Contains(parts[0]) || parts[1].Length == 0)
        {
            throw Fail("path", "Fayl yolu etibarsızdır.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(MediaRoot, parts[0], parts[1]));
        if (!fullPath.StartsWith(MediaRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new NotFoundException("Fayl tapılmadı.");
        }

        return (relative, fullPath);
    }

    private static string SlotLabel(string slot)
        => Slots.TryGetValue(slot ?? string.Empty, out var label) ? label : throw Fail("slot", "Şəkil yeri tanınmır.");

    private static ValidationException Fail(string field, string message)
        => new(new[] { new ValidationFailure(field, message) });
}
