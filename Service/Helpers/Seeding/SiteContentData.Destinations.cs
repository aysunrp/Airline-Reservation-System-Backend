namespace Service.Helpers.Seeding;

/// <summary>The headings of the destinations pages. Keys are the <c>data-i18n</c> paths used in destinations.html and destination-details.html.</summary>
public static partial class SiteContentData
{
    public const string DestinationsPage = "destinations";

    public static readonly IReadOnlyList<Item> Destinations = new List<Item>
    {
        new("destinations.guideEyebrow", "İstiqamət bələdçisi"),
        new("destinations.allDestinationsTitle", "Bütün təyinatlar"),
        new("destinations.allDestinationsText", "AEROVA-nın apara biləcəyi hər ölkə və səfərə başlanacaq şəhər."),
        new("destinations.nextTitle", "Növbəti haraya gedəcəksiniz?"),
        new("destinations.nextText", "Üfüq seçin, gediş tarixini təyin edin və sakit dəqiqliklə səyahətə başlayın."),
        new("destinations.aboutTitle", "Təyinat haqqında"),
        new("destinations.backToDestinations", "Təyinatlara qayıt"),
    };
}
