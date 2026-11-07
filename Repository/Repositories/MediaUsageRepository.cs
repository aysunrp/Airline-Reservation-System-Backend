using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class MediaUsageRepository : IMediaUsageRepository
{
    private readonly AppDbContext _context;

    public MediaUsageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MediaReference>> GetReferencesAsync(CancellationToken cancellationToken = default)
    {
        var references = new List<MediaReference>();

        references.AddRange((await _context.HomeCards.AsNoTracking().Where(x => x.ImagePath != null)
            .Select(x => new { x.ImagePath, x.Title }).ToListAsync(cancellationToken))
            .Select(x => new MediaReference(x.ImagePath!, "Ana səhifə kartı: " + x.Title)));

        references.AddRange((await _context.Countries.AsNoTracking().Where(x => x.ImageFile != "")
            .Select(x => new { x.ImageFile, x.NameAz }).ToListAsync(cancellationToken))
            .Select(x => new MediaReference(x.ImageFile, "Ölkə: " + x.NameAz)));

        references.AddRange((await _context.DestinationCities.AsNoTracking().Where(x => x.ImageFile != "")
            .Select(x => new { x.ImageFile, x.NameAz }).ToListAsync(cancellationToken))
            .Select(x => new MediaReference(x.ImageFile, "Şəhər: " + x.NameAz)));

        foreach (var video in await _context.LandingHeroVideos.AsNoTracking().ToListAsync(cancellationToken))
        {
            foreach (var url in new[] { video.VideoUrl, video.PosterUrl, video.MobileVideoUrl })
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    references.Add(new MediaReference(url, "Hero video: " + video.Title));
                }
            }
        }

        references.AddRange((await _context.SiteContents.AsNoTracking()
            .Where(x => x.Page == "site" && x.Key.StartsWith("image."))
            .Select(x => new { x.Key, x.TextAz }).ToListAsync(cancellationToken))
            .Select(x => new MediaReference(x.TextAz, "Sayt şəkli: " + x.Key)));

        return references;
    }
}
