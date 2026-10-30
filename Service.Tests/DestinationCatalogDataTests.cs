using System.Text.RegularExpressions;
using Service.Helpers.Seeding;

namespace Service.Tests;

/// <summary>The destination catalog data itself: the intended 20 countries, valid airport codes and zones, no duplicates, three languages everywhere.</summary>
public class DestinationCatalogDataTests
{
    private static readonly string[] ExpectedCountries =
    {
        "AZ", "TR", "AE", "GB", "FR", "DE", "IT", "ES", "GE", "QA", "SA", "CH", "NL", "US", "CA", "JP", "CN", "SG", "TH", "MV"
    };

    private static IEnumerable<CatalogCitySeed> Cities => DestinationCatalogData.Countries.SelectMany(c => c.Cities);
    private static IEnumerable<CatalogAirportSeed> Airports => Cities.SelectMany(c => c.Airports);

    [Fact]
    public void TheCatalog_HasExactlyTheTwentyIntendedCountries_InTheAgreedOrder()
        => Assert.Equal(ExpectedCountries, DestinationCatalogData.Countries.Select(c => c.Code));

    [Fact]
    public void TheCatalog_HasThe37AgreedCities_AndEachCountryItsOwn()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["AZ"] = new[] { "Baku", "Ganja" }, ["TR"] = new[] { "Istanbul", "Ankara", "Antalya" }, ["AE"] = new[] { "Dubai", "Abu Dhabi" },
            ["GB"] = new[] { "London", "Manchester" }, ["FR"] = new[] { "Paris", "Nice" }, ["DE"] = new[] { "Berlin", "Frankfurt" },
            ["IT"] = new[] { "Rome", "Milan" }, ["ES"] = new[] { "Madrid", "Barcelona" }, ["GE"] = new[] { "Tbilisi", "Batumi" },
            ["QA"] = new[] { "Doha" }, ["SA"] = new[] { "Riyadh", "Jeddah" }, ["CH"] = new[] { "Zurich", "Geneva" }, ["NL"] = new[] { "Amsterdam" },
            ["US"] = new[] { "New York", "Los Angeles" }, ["CA"] = new[] { "Toronto", "Montreal" }, ["JP"] = new[] { "Tokyo", "Osaka" },
            ["CN"] = new[] { "Beijing", "Shanghai" }, ["SG"] = new[] { "Singapore" }, ["TH"] = new[] { "Bangkok", "Phuket" }, ["MV"] = new[] { "Malé" }
        };

        foreach (var country in DestinationCatalogData.Countries)
        {
            Assert.Equal(expected[country.Code], country.Cities.Select(c => c.Name));
        }

        Assert.Equal(37, Cities.Count());
        Assert.Equal(38, Airports.Count());
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
    public void EveryCountryAndCity_IsNamedAndDescribedInAllThreeLanguages_WithItsOwnPhotograph()
    {
        Assert.All(DestinationCatalogData.Countries, c =>
        {
            Assert.Matches("^[A-Z]{2}$", c.Code);
            Assert.All(new[] { c.Name, c.NameAz, c.NameRu, c.Description, c.DescriptionAz, c.DescriptionRu }, text => Assert.False(string.IsNullOrWhiteSpace(text)));
            Assert.InRange(c.Description.Length, 20, 400);
            Assert.Matches(new Regex("^[a-z0-9-]+\\.jpg$"), c.ImageFile);
        });
        Assert.All(Cities, c => Assert.All(new[] { c.Name, c.NameAz, c.NameRu }, text => Assert.False(string.IsNullOrWhiteSpace(text))));

        Assert.Equal(20, DestinationCatalogData.Countries.Select(c => c.ImageFile).Distinct().Count()); // no photograph twice
        Assert.Equal(20, DestinationCatalogData.Countries.Select(c => c.Name).Distinct().Count());
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
