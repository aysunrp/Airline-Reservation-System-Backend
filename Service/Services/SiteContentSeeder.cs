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
        var added = 0;
        var alreadyPresent = 0;

        foreach (var page in SiteContentData.GetSeedPages())
        {
            var existing = await _contents.GetKeysAsync(page.Page, cancellationToken);
            foreach (var item in page.Items)
            {
                if (existing.Contains(item.Key))
                {
                    alreadyPresent++;
                    continue;
                }

                await _contents.AddAsync(new SiteContent
                {
                    Page = page.Page, Key = item.Key, TextAz = item.TextAz
                }, cancellationToken);
                added++;
            }
        }

        if (added > 0)
        {
            await _contents.SaveChangesAsync(cancellationToken);
        }

        return new SiteContentSeedResultDto(added, alreadyPresent);
    }
}
