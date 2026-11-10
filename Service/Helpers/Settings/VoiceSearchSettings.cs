namespace Service.Helpers.Settings;

/// <summary>Voice flight search: how a spoken request is read and how wide a search without a date is. Nothing here is secret.</summary>
public class VoiceSearchSettings
{
    public const string SectionName = "VoiceSearch";

    /// <summary>Days searched when the traveller asks for the cheapest flight, or for flights, without saying when (today included).</summary>
    public int DefaultRangeDays { get; set; } = 30;

    /// <summary>The widest period one search may cover (the price calendar uses the same limit).</summary>
    public int MaxRangeDays { get; set; } = 62;

    /// <summary>Most flights one period search returns (cheapest first).</summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>The longest request text that is read.</summary>
    public int MaxTextLength { get; set; } = 300;

    /// <summary>When the rules find no route, the travel assistant (Gemini) may be asked to read the request. Without a key this does nothing.</summary>
    public bool UseAssistantForComplexRequests { get; set; } = true;
}
