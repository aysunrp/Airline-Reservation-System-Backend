using Domain.Enums;

namespace Service.Helpers.DTOs.Flight;

/// <summary>
/// A part of the day, read at the origin airport. The ranges are fixed so the API and the results page agree:
/// morning 05:00-11:59, afternoon 12:00-17:59, evening 18:00-23:59, night 00:00-04:59.
/// </summary>
public enum DepartureTimeOfDay
{
    Morning,
    Afternoon,
    Evening,
    Night
}

public static class DepartureTimeOfDayRanges
{
    public static bool Contains(DepartureTimeOfDay part, TimeOnly time) => part switch
    {
        DepartureTimeOfDay.Morning => time.Hour is >= 5 and < 12,
        DepartureTimeOfDay.Afternoon => time.Hour is >= 12 and < 18,
        DepartureTimeOfDay.Evening => time.Hour >= 18,
        _ => time.Hour < 5
    };
}

/// <summary>The text recognised by the browser (or typed), and the language of the site.</summary>
public class VoiceSearchRequestDto
{
    public string Text { get; set; } = string.Empty;

    /// <summary>"az", "en" or "ru": used when the words alone do not show the language.</summary>
    public string? Language { get; set; }
}

public record VoiceAirportDto(string Code, string City, string Name);

/// <summary>
/// Something the traveller has to clear up before the search can run. <c>Code</c> is one of: missing_text, missing_origin,
/// missing_destination, same_airport, ambiguous_origin, ambiguous_destination (Options lists the airports), unknown_city
/// (Value is the name), past_date (Value is the date), invalid_date, ambiguous_date, bad_passengers (Value is the number).
/// The page turns the code into a sentence in the language of the site.
/// </summary>
public record VoiceIssueDto(string Code, string? Value = null, IReadOnlyList<VoiceAirportDto>? Options = null);

/// <summary>The search criteria read from a spoken request. Nothing here is a flight: the page searches the real schedules with it.</summary>
public record VoiceSearchInterpretationDto(
    string Status,
    string Language,
    VoiceAirportDto? Origin,
    VoiceAirportDto? Destination,
    DateOnly? Date,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    DepartureTimeOfDay? TimeOfDay,
    CabinClass? CabinClass,
    int Passengers,
    bool PassengersSpecified,
    bool Cheapest,
    bool CheapestSingle,
    bool DefaultPeriod,
    string InterpretedBy,
    IReadOnlyList<VoiceIssueDto> Issues)
{
    public const string Ready = "ready";
    public const string NeedsClarification = "needs_clarification";
}

/// <summary>All bookable flights of one route over a period (at most MaxRangeDays), cheapest or earliest first.</summary>
public class FlightRangeSearchRequestDto
{
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;

    /// <summary>First departure day at the origin airport (included).</summary>
    public DateOnly From { get; set; }

    /// <summary>Last departure day at the origin airport (included).</summary>
    public DateOnly To { get; set; }

    public CabinClass? CabinClass { get; set; }
    public int Passengers { get; set; } = 1;

    /// <summary>When set, only flights that leave in this part of the day (origin airport time) are returned.</summary>
    public DepartureTimeOfDay? TimeOfDay { get; set; }

    /// <summary>"price" (cheapest first, the default) or "departure" (earliest first).</summary>
    public string SortBy { get; set; } = "price";

    /// <summary>
    /// How many flights to return (1 to VoiceSearch:MaxResults). The whole period is still compared first, so Limit 1 with sortBy "price" is the one
    /// cheapest flight of the period. Empty means VoiceSearch:MaxResults.
    /// </summary>
    public int? Limit { get; set; }
}

public record FlightRangeSearchResultDto(
    string OriginCode,
    string DestinationCode,
    DateOnly From,
    DateOnly To,
    string SortBy,
    DepartureTimeOfDay? TimeOfDay,
    int TotalFound,
    int LowestPriceCount,
    IReadOnlyList<FlightResultDto> Flights);
