using System.Text.RegularExpressions;
using Service.Helpers.Seeding;

namespace Service.Tests;

/// <summary>The destination catalog data itself: the 15 countries, valid airport codes and zones, no duplicates, three languages everywhere.</summary>
public class DestinationCatalogDataTests
{
    /// <summary>The countries of the network, in the order the catalog shows them.</summary>
    private static readonly string[] ExpectedCountries =
    {
        "AZ", "TR", "AE", "GB", "FR", "DE", "IT", "ES", "JP", "MV", "AT", "US", "GR", "TH", "PT"
    };

    /// <summary>These countries have no photograph of their own yet and show a general aviation photograph.</summary>
    private static readonly string[] CountriesWithoutPhotograph = { "AT" };

    private static IEnumerable<CatalogCitySeed> Cities => DestinationCatalogData.Countries.SelectMany(c => c.Cities);
    private static IEnumerable<CatalogAirportSeed> Airports => Cities.SelectMany(c => c.Airports);

    [Fact]
    public void TheCatalog_HasExactlyTheFifteenIntendedCountries_InTheAgreedOrder()
        => Assert.Equal(ExpectedCountries, DestinationCatalogData.Countries.Select(c => c.Code));

    [Fact]
    public void TheCatalog_HasThe38AgreedCities_AndEachCountryItsOwn()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["AZ"] = new[] { "Baku", "Ganja", "Nakhchivan" },
            ["TR"] = new[] { "Istanbul", "Antalya", "Bodrum" },
            ["AE"] = new[] { "Dubai", "Abu Dhabi" },
            ["GB"] = new[] { "London", "Manchester", "Edinburgh" },
            ["FR"] = new[] { "Paris", "Marseille" },
            ["DE"] = new[] { "Berlin", "Frankfurt", "Munich" },
            ["IT"] = new[] { "Rome", "Milan", "Venice" },
            ["ES"] = new[] { "Madrid", "Barcelona", "Malaga" },
            ["JP"] = new[] { "Tokyo", "Osaka" },
            ["MV"] = new[] { "Malé" },
            ["AT"] = new[] { "Vienna", "Salzburg" },
            ["US"] = new[] { "New York", "Los Angeles", "Miami" },
            ["GR"] = new[] { "Athens", "Santorini", "Mykonos" },
            ["TH"] = new[] { "Bangkok", "Phuket" },
            ["PT"] = new[] { "Lisbon", "Porto", "Faro" }
        };

        foreach (var country in DestinationCatalogData.Countries)
        {
            Assert.Equal(expected[country.Code], country.Cities.Select(c => c.Name));
        }

        Assert.Equal(38, Cities.Count());
        Assert.Equal(38, Airports.Count()); // one airport per city
    }

    [Fact]
    public void Azerbaijan_HasOnlyBakuNakhchivanAndGanja_AndTheMaldivesOnlyMale()
    {
        var azerbaijan = DestinationCatalogData.Countries.Single(c => c.Code == "AZ");
        Assert.Equal(new[] { "GYD", "GNJ", "NAJ" }, azerbaijan.Cities.SelectMany(c => c.Airports).Select(a => a.Code));

        var maldives = DestinationCatalogData.Countries.Single(c => c.Code == "MV");
        Assert.Equal("MLE", Assert.Single(Assert.Single(maldives.Cities).Airports).Code);
    }

    [Fact]
    public void TheCountriesLeftOutOfTheNetwork_AreSwitchedOffNotRemoved()
    {
        Assert.Equal(new[] { "QA", "SA", "CH", "CA", "CN", "SG", "GE", "NL", "PL", "KZ", "UZ" }, DestinationCatalogData.RetiredCountryCodes);
        Assert.Empty(DestinationCatalogData.RetiredCountryCodes.Intersect(DestinationCatalogData.Countries.Select(c => c.Code)));
        Assert.Empty(DestinationCatalogData.RetiredAirportCodes.Intersect(Airports.Select(a => a.Code)));
        Assert.Equal(new[] { "US", "TH" }, DestinationCatalogData.ReactivatedCountryCodes);
        Assert.All(DestinationCatalogData.ReactivatedCountryCodes, code => Assert.Contains(code, DestinationCatalogData.Countries.Select(c => c.Code)));
    }

    [Fact]
    public void EveryAirport_HasAnIataCodeOnce_AndAValidTimeZone()
    {
        Assert.All(Airports, a => Assert.Matches("^[A-Z]{3}$", a.Code));
        Assert.Equal(Airports.Count(), Airports.Select(a => a.Code).Distinct().Count());
        Assert.All(Airports, a => Assert.NotNull(TimeZoneInfo.FindSystemTimeZoneById(a.TimeZoneId)));
        Assert.All(Airports, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
    }

    [Fact]
    public void EveryCountryAndCity_IsNamedAndDescribedInAllThreeLanguages()
    {
        Assert.All(DestinationCatalogData.Countries, c =>
        {
            Assert.Matches("^[A-Z]{2}$", c.Code);
            Assert.All(new[] { c.Name, c.NameAz, c.NameRu, c.Description, c.DescriptionAz, c.DescriptionRu }, text => Assert.False(string.IsNullOrWhiteSpace(text)));
            Assert.InRange(c.Description.Length, 20, 400);
            Assert.True(c.ImageFile.Length == 0 || Regex.IsMatch(c.ImageFile, "^[a-z0-9-]+\\.jpg$")); // empty until the photograph is chosen
        });
        Assert.All(Cities, c => Assert.All(new[] { c.Name, c.NameAz, c.NameRu }, text => Assert.False(string.IsNullOrWhiteSpace(text))));
        Assert.All(Cities, c => Assert.All(new[] { c.Description, c.DescriptionAz, c.DescriptionRu }, text => Assert.InRange(text.Length, 20, 400)));

        Assert.Equal(15, DestinationCatalogData.Countries.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void ACityWithoutItsOwnPhotograph_ShowsTheOneOfItsCountry_AndNoPhotographIsUsedTwice()
    {
        // A city either has a photograph of its own (a file in assets/images) or none (the page then shows its country's photograph).
        Assert.All(Cities, c => Assert.True(c.ImageFile.Length == 0 || Regex.IsMatch(c.ImageFile, "^[a-z0-9-]+\\.jpg$")));

        // Countries with a photograph: it is the one of the first city, so a country card and its city card agree.
        Assert.All(DestinationCatalogData.Countries, c => Assert.Equal(c.ImageFile, c.Cities[0].ImageFile));

        // Every photograph belongs to one place, except the general one shared by the countries that have none yet.
        var photographs = DestinationCatalogData.Countries.Where(c => !CountriesWithoutPhotograph.Contains(c.Code)).SelectMany(c => c.Cities.Select(city => city.ImageFile)).Where(f => f.Length > 0).ToList();
        Assert.Equal(photographs.Count, photographs.Distinct().Count());
        Assert.Single(DestinationCatalogData.Countries.Where(c => CountriesWithoutPhotograph.Contains(c.Code)).Select(c => c.ImageFile).Distinct());
    }

    [Fact]
    public void EveryCityOfTurkeyHasOneAirport()
    {
        var turkey = DestinationCatalogData.Countries.Single(c => c.Code == "TR");
        Assert.Equal(new[] { "IST" }, turkey.Cities.Single(c => c.Name == "Istanbul").Airports.Select(a => a.Code));
        Assert.Equal(new[] { "BJV" }, turkey.Cities.Single(c => c.Name == "Bodrum").Airports.Select(a => a.Code));
        Assert.Equal(new[] { "AYT" }, turkey.Cities.Single(c => c.Name == "Antalya").Airports.Select(a => a.Code));
    }

    [Fact]
    public void BakuIsTheOnlyHub_TheHomeBaseOfTheAirline()
    {
        var hub = Assert.Single(Cities, c => c.IsHub);
        Assert.Equal("Baku", hub.Name);
        Assert.Equal("GYD", Assert.Single(hub.Airports).Code);
    }

    [Fact]
    public void TheCatalog_KeepsNoDuplicateCityInACountry()
        => Assert.All(DestinationCatalogData.Countries, c => Assert.Equal(c.Cities.Length, c.Cities.Select(city => city.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()));
}
