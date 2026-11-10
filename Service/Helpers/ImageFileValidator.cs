using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace Service.Helpers;

/// <summary>
/// Checks that an uploaded image really is one: the type the browser sent must be an allowed image type, and the first bytes of the file
/// must be those of a JPEG, a PNG or a WebP image. The extension alone (a script renamed to photo.png) is not trusted.
/// </summary>
public static class ImageFileValidator
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public static async Task EnsureImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.Contains(file.ContentType ?? string.Empty))
        {
            throw Fail("Fayl növü dəstəklənmir: yalnız JPG, PNG və ya WEBP şəkli yükləyin.");
        }

        var header = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        }

        if (!LooksLikeImage(header, read))
        {
            throw Fail("Fayl şəkil deyil: məzmunu JPG, PNG və ya WEBP olmalıdır.");
        }
    }

    internal static bool LooksLikeImage(byte[] h, int length)
    {
        var jpeg = length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
        var png = length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A;
        var webp = length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';
        return jpeg || png || webp;
    }

    private static ValidationException Fail(string message) => new(new[] { new ValidationFailure("file", message) });
}
