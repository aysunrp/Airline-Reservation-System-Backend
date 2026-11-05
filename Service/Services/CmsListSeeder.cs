using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Adds the home cards and the support questions once. A marker row records that it was done, so when an admin later deletes every card
/// or every question, a restart does not bring them back. It never changes what exists.
/// </summary>
public class CmsListSeeder : ICmsListSeeder
{
    public const string MarkerPage = "system";
    public const string FaqMarker = "faq-seeded";
    public const string CardsMarker = "home-cards-seeded";

    private readonly IFaqItemRepository _faq;
    private readonly IHomeCardRepository _cards;
    private readonly ISiteContentRepository _contents;

    public CmsListSeeder(IFaqItemRepository faq, IHomeCardRepository cards, ISiteContentRepository contents)
    {
        _faq = faq;
        _cards = cards;
        _contents = contents;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var added = 0;
        var done = await _contents.GetKeysAsync(MarkerPage, cancellationToken);

        if (!done.Contains(FaqMarker))
        {
            if (await _faq.CountAsync(cancellationToken) == 0)
            {
                var order = 1;
                foreach (var item in CmsListSeedData.Faq)
                {
                    await _faq.AddAsync(new FaqItem { Question = item.Question, Answer = item.Answer, SortOrder = order++, IsActive = true }, cancellationToken);
                    added++;
                }

                await _faq.SaveChangesAsync(cancellationToken);
            }

            await _contents.AddAsync(new SiteContent { Page = MarkerPage, Key = FaqMarker, TextAz = "1" }, cancellationToken);
        }

        if (!done.Contains(CardsMarker))
        {
            if (await _cards.CountAsync(cancellationToken) == 0)
            {
                foreach (var seed in CmsListSeedData.Cards)
                {
                    await _cards.AddAsync(new HomeCard
                    {
                        Section = seed.Section, SortOrder = seed.SortOrder, Title = seed.Title, Description = seed.Description, Label = seed.Label,
                        LinkText = seed.LinkText, LinkUrl = seed.LinkUrl, IconKey = seed.IconKey, ImagePath = seed.ImagePath, ImageAlt = seed.ImageAlt,
                        IsActive = true
                    }, cancellationToken);
                    added++;
                }

                await _cards.SaveChangesAsync(cancellationToken);
            }

            await _contents.AddAsync(new SiteContent { Page = MarkerPage, Key = CardsMarker, TextAz = "1" }, cancellationToken);
        }

        await _contents.SaveChangesAsync(cancellationToken);
        return added;
    }
}
