namespace Service.Helpers.Seeding;

/// <summary>
/// The links of the site that an admin can change: the hero button and the social networks of the footer. They are what the pages
/// linked to before they were stored in the database. Contact details (e-mail, phone, address) are not seeded: the site has none yet, and
/// the admin panel adds them when they are filled in.
/// </summary>
public static partial class SiteContentData
{
    public const string SitePage = "site";

    public static readonly IReadOnlyList<Item> Site = new List<Item>
    {
        new("hero.ctaUrl", "booking.html"),
        new("social.facebook", "https://www.facebook.com/"),
        new("social.instagram", "https://www.instagram.com/"),
        new("social.x", "https://x.com/"),
        new("social.youtube", "https://www.youtube.com/"),
        new("social.linkedin", "https://www.linkedin.com/"),
    };
}
