using Domain.Enums;

namespace Service.Helpers.Settings;

/// <summary>
/// Which travel documents are accepted on a route, the same rules the passenger form uses.
/// A flight whose airports are all in one country is domestic and uses <see cref="Domestic"/>; otherwise every
/// country after the departure one (stops and destination) must accept the document, and the strictest
/// minimum validity applies. Anything not configured falls back to a passport with no extra validity.
/// </summary>
public class TravelDocumentSettings
{
    public const string SectionName = "TravelDocuments";

    /// <summary>Rules for flights inside one country, by ISO country code.</summary>
    public Dictionary<string, CountryDocumentRule> Domestic { get; set; } = new();

    /// <summary>Rules for entering a country, by ISO country code.</summary>
    public Dictionary<string, CountryDocumentRule> International { get; set; } = new();

    /// <summary>Applies to every country on an international route that has no rule of its own.</summary>
    public CountryDocumentRule InternationalDefault { get; set; } = new();
}

public class CountryDocumentRule
{
    /// <summary>Documents accepted from travellers of any other nationality.</summary>
    public List<DocumentType> Documents { get; set; } = new();

    /// <summary>Documents accepted from travellers of a given nationality (ISO country code).</summary>
    public Dictionary<string, List<DocumentType>> ByNationality { get; set; } = new();

    /// <summary>Days the document must stay valid after the last travel date.</summary>
    public int MinValidityDays { get; set; }
}
