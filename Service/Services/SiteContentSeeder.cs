using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Adds <see cref="SiteContentData"/>. A text is found by (page, key); what exists is left as it is, so the seed can run any number of
/// times and never overwrites a change made in the admin panel.
/// </summary>
public class SiteContentSeeder : ISiteContentSeeder
{
    private readonly ISiteContentRepository _contents;

    public SiteContentSeeder(ISiteContentRepository contents) => _contents = contents;

    public async Task<SiteContentSeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _contents.GetKeysAsync(SiteContentData.AboutPage, cancellationToken);
        var added = 0;
        foreach (var item in SiteContentData.About.Where(i => !existing.Contains(i.Key)))
        {
            await _contents.AddAsync(new SiteContent
            {
                Page = SiteContentData.AboutPage, Key = item.Key, TextAz = item.TextAz
            }, cancellationToken);
            added++;
        }

        if (added > 0)
        {
            await _contents.SaveChangesAsync(cancellationToken);
        }

        return new SiteContentSeedResultDto(added, SiteContentData.About.Count - added);
    }
}
