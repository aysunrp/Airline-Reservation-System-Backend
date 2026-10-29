using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Scheduling;
using Service.Helpers.Settings;

namespace Service.Services.VoiceSearch;

/// <summary>
/// Reads a spoken or typed flight request in Azerbaijani, English or Russian with fixed rules (no AI, nothing leaves the server):
/// the route, the day or period, a part of the day, the cabin, the number of passengers and a wish for the cheapest flight.
/// It finds criteria only: the flights come from the real schedules. What it cannot be sure of is returned as an issue to clear up,
/// never guessed.
/// </summary>
public sealed class VoiceSearchInterpreter
{
    private static readonly Regex IsoDate = new(@"(?<!\d)(\d{4})-(\d{1,2})-(\d{1,2})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex NumericDatePattern = new(@"(?<!\d)(\d{1,2})([./])(\d{1,2})(?:\2(\d{4}))?(?!\d)", RegexOptions.Compiled);
    private static readonly Regex DayToken = new(@"^(\d{1,2})(st|nd|rd|th)?$", RegexOptions.Compiled);
    private static readonly Regex YearToken = new(@"^(20\d{2})$", RegexOptions.Compiled);
    private static readonly HashSet<string> DayWords = new(new[] { "day", "days", "gun", "gunluk", "дни", "дня", "дней", "день" }.Select(VoiceText.Fold));
    private static readonly HashSet<string> Ordinals = new(VoiceLexicon.Ordinals.Select(VoiceText.Fold));

    private readonly VoiceSearchSettings _settings;

    public VoiceSearchInterpreter(VoiceSearchSettings settings)
    {
        _settings = settings;
    }

    /// <summary>A city found in the words, with the airports it may mean.</summary>
    internal sealed record Mention(int Start, int Length, IReadOnlyList<Airport> Airports, string Suffix, bool ByCode);

    public VoiceSearchInterpretationDto Interpret(string? text, string? language, IReadOnlyList<Airport> airports, DateTime utcNow)
    {
        var issues = new List<VoiceIssueDto>();
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length > _settings.MaxTextLength)
        {
            raw = raw[.._settings.MaxTextLength];
        }

        var votes = new Dictionary<string, int> { ["az"] = 0, ["en"] = 0, ["ru"] = 0 };
        if (raw.Any(c => c is 'ə' or 'Ə' or 'ı' or 'İ' or 'ş' or 'Ş' or 'ğ' or 'Ğ'))
        {
            votes["az"] += 2;
        }

        votes["ru"] += raw.Count(c => c is >= 'А' and <= 'я' or 'ё' or 'Ё') > 2 ? 3 : 0;

        // Dates written with digits are taken out of the text first, so their numbers are not read as anything else.
        var numericDates = ExtractNumericDates(ref raw, language, issues);

        var words = VoiceText.Words(raw);
        var tokens = words.Select(w => w.Folded).ToArray();
        var used = new bool[tokens.Length];

        if (tokens.Length == 0 && numericDates.Count == 0)
        {
            return Build(null, null, null, null, null, null, null, 1, false, false, false, false, "en",
                new List<VoiceIssueDto> { new("missing_text") });
        }

        var active = airports.Where(a => a.IsActive).ToList();
        var (origin, destination) = ReadRoute(tokens, words, used, active, votes, issues);

        var (passengers, passengersSpecified) = ReadPassengers(tokens, used, votes, issues);
        var cabin = ReadFirst(VoiceLexicon.Cabins, tokens, used, votes);
        var cheapest = ReadFirst(VoiceLexicon.Cheapest, tokens, used, votes) is true;
        // "the cheapest flight" is one flight; "the cheapest flights" (a plural noun) keeps several low-priced options.
        var single = cheapest && !tokens.Any(VoiceLexicon.PluralWords.Contains);

        var today = TodayAt(origin, utcNow);
        var lang = PickLanguage(votes, language);
        var period = ReadWhen(tokens, used, votes, today, lang, numericDates, issues, cheapest);
        var timeOfDay = period.TimeOfDay ?? ReadFirst(VoiceLexicon.TimesOfDay, tokens, used, votes);

        if (origin is not null && destination is not null && origin.Id == destination.Id)
        {
            issues.Add(new VoiceIssueDto("same_airport", origin.Code));
        }

        DateOnly? date = null, from = null, to = null;
        var defaultPeriod = false;
        if (period.Date is { } day)
        {
            // The cheapest flight of one named day is still a price search, so it uses the period search with a single day.
            if (cheapest)
            {
                from = to = day;
            }
            else
            {
                date = day;
            }
        }
        else if (period.From is { } first && period.To is { } last)
        {
            from = first;
            to = last;
        }
        else if (!issues.Any(i => i.Code is "past_date" or "invalid_date" or "ambiguous_date"))
        {
            // No day was said: search the coming days, and say so.
            from = today;
            to = today.AddDays(Math.Max(1, _settings.DefaultRangeDays) - 1);
            defaultPeriod = true;
        }

        if (from is not null && to is not null)
        {
            var span = Math.Max(1, _settings.MaxRangeDays);
            if (to.Value.DayNumber - from.Value.DayNumber >= span)
            {
                to = from.Value.AddDays(span - 1);
            }
        }

        return Build(origin, destination, date, from, to, timeOfDay, cabin, passengers, passengersSpecified, cheapest, single, defaultPeriod, lang, issues);
    }

    private static string PickLanguage(Dictionary<string, int> votes, string? requested)
    {
        var prior = requested?.ToLowerInvariant() is "az" or "en" or "ru" ? requested.ToLowerInvariant() : "en";
        var top = votes.Values.Max();
        if (top == 0 || votes[prior] == top)
        {
            return prior;
        }

        return votes.First(v => v.Value == top).Key;
    }

    private static VoiceSearchInterpretationDto Build(
        Airport? origin, Airport? destination, DateOnly? date, DateOnly? from, DateOnly? to, DepartureTimeOfDay? timeOfDay, CabinClass? cabin,
        int passengers, bool passengersSpecified, bool cheapest, bool cheapestSingle, bool defaultPeriod, string detected, List<VoiceIssueDto> issues)
    {
        var told = (string code) => issues.Any(i => i.Code == code);
        if (!told("missing_text"))
        {
            if (origin is null && !told("ambiguous_origin") && !told("unknown_origin"))
            {
                issues.Insert(0, new VoiceIssueDto("missing_origin"));
            }

            if (destination is null && !told("ambiguous_destination") && !told("unknown_destination"))
            {
                issues.Add(new VoiceIssueDto("missing_destination"));
            }
        }

        return new VoiceSearchInterpretationDto(
            issues.Count == 0 ? VoiceSearchInterpretationDto.Ready : VoiceSearchInterpretationDto.NeedsClarification,
            detected, ToDto(origin), ToDto(destination), date, from, to, timeOfDay, cabin, passengers, passengersSpecified, cheapest, cheapestSingle, defaultPeriod,
            "rules", issues);
    }

    internal static VoiceAirportDto? ToDto(Airport? airport) => airport is null ? null : new VoiceAirportDto(airport.Code, airport.City, airport.Name);

    private static DateOnly TodayAt(Airport? origin, DateTime utcNow)
    {
        if (origin is not null && TimeZoneInfo.TryFindSystemTimeZoneById(origin.TimeZoneId, out _))
        {
            return ScheduleTimeCalculator.LocalDate(utcNow, origin);
        }

        return DateOnly.FromDateTime(utcNow);
    }

    // ---- cities ----

    private (Airport? Origin, Airport? Destination) ReadRoute(
        string[] tokens, List<(string Folded, string Original)> words, bool[] used, List<Airport> airports, Dictionary<string, int> votes, List<VoiceIssueDto> issues)
    {
        var mentions = FindMentions(tokens, words, airports);
        foreach (var m in mentions)
        {
            for (var i = m.Start; i < m.Start + m.Length; i++)
            {
                used[i] = true;
            }

            if (VoiceLexicon.OriginSuffixes.Contains(m.Suffix) || VoiceLexicon.DestinationSuffixes.Contains(m.Suffix) && m.Suffix.Length > 1)
            {
                votes["az"]++;
            }
        }

        Mention? origin = null, destination = null;
        var unmarked = new List<Mention>();
        foreach (var m in mentions)
        {
            var before = m.Start > 0 ? tokens[m.Start - 1] : string.Empty;
            var role = VoiceLexicon.OriginSuffixes.Contains(m.Suffix) || VoiceLexicon.OriginMarkers.Contains(before) ? 'O'
                : VoiceLexicon.DestinationSuffixes.Contains(m.Suffix) && m.Suffix.Length > 0 && !m.ByCode || VoiceLexicon.DestinationMarkers.Contains(before) ? 'D'
                : ' ';

            if (role == 'O' && origin is null)
            {
                origin = m;
            }
            else if (role == 'D' && destination is null)
            {
                destination = m;
            }
            else if (role == ' ')
            {
                unmarked.Add(m);
            }
        }

        foreach (var m in unmarked)
        {
            if (origin is null && destination is null)
            {
                origin = m;
            }
            else if (origin is null)
            {
                origin = m;
            }
            else if (destination is null)
            {
                destination = m;
            }
        }

        // A lone unmarked city ("London tomorrow") is the place to fly to.
        if (origin is not null && destination is null && mentions.Count == 1 && unmarked.Count == 1)
        {
            // "Baku to ..." is the place to leave from; only a city with nothing after it that says "to" is where the traveller wants to go.
            var after = origin.Start + origin.Length;
            if (after >= tokens.Length || !VoiceLexicon.DestinationMarkers.Contains(tokens[after]))
            {
                destination = origin;
                origin = null;
            }
        }

        var originAirport = Resolve(origin, "ambiguous_origin", issues);
        var destinationAirport = Resolve(destination, "ambiguous_destination", issues);
        ReadUnknownCities(tokens, used, originAirport is null, destinationAirport is null, issues);
        return (originAirport, destinationAirport);
    }

    private static Airport? Resolve(Mention? mention, string ambiguousCode, List<VoiceIssueDto> issues)
    {
        if (mention is null)
        {
            return null;
        }

        if (mention.Airports.Count == 1)
        {
            return mention.Airports[0];
        }

        issues.Add(new VoiceIssueDto(ambiguousCode, mention.Airports[0].City, mention.Airports.Select(ToDto).OfType<VoiceAirportDto>().ToList()));
        return null;
    }

    private static List<Mention> FindMentions(string[] tokens, List<(string Folded, string Original)> words, List<Airport> airports)
    {
        // Every name a city goes by, as words, with the airports it stands for.
        var names = new List<(string[] Words, List<Airport> Airports)>();
        foreach (var group in airports.GroupBy(a => VoiceText.Fold(a.City).Trim()))
        {
            var set = group.ToList();
            var spellings = new HashSet<string> { group.Key };
            foreach (var airport in set)
            {
                if (VoiceLexicon.CityAliases.TryGetValue(airport.Code, out var aliases))
                {
                    foreach (var alias in aliases)
                    {
                        spellings.Add(VoiceText.Fold(alias).Trim());
                    }
                }
            }

            foreach (var spelling in spellings.Where(s => s.Length > 0))
            {
                names.Add((spelling.Split(' ', StringSplitOptions.RemoveEmptyEntries), set));
            }
        }

        var found = new List<Mention>();
        var i = 0;
        while (i < tokens.Length)
        {
            Mention? best = null;
            foreach (var (nameWords, set) in names)
            {
                var last = nameWords.Length - 1;
                if (i + last >= tokens.Length)
                {
                    continue;
                }

                var matches = true;
                for (var k = 0; k < last && matches; k++)
                {
                    matches = tokens[i + k] == nameWords[k];
                }

                if (!matches)
                {
                    continue;
                }

                var token = tokens[i + last];
                if (!token.StartsWith(nameWords[last], StringComparison.Ordinal))
                {
                    continue;
                }

                var suffix = token[nameWords[last].Length..];
                if (!VoiceLexicon.CitySuffixes.Contains(suffix) || nameWords[last].Length < 3 && suffix.Length > 0)
                {
                    continue;
                }

                if (best is null || nameWords.Length > best.Length)
                {
                    best = new Mention(i, nameWords.Length, set, suffix, false);
                }
            }

            if (best is null)
            {
                // An airport code typed in capitals ("GYD").
                var original = words[i].Original;
                var byCode = original.Length == 3 && original.All(char.IsUpper)
                    ? airports.FirstOrDefault(a => string.Equals(a.Code, original, StringComparison.Ordinal))
                    : null;
                if (byCode is not null)
                {
                    best = new Mention(i, 1, new List<Airport> { byCode }, string.Empty, true);
                }
            }

            if (best is not null)
            {
                found.Add(best);
                i += best.Length;
            }
            else
            {
                i++;
            }
        }

        return found;
    }

    /// <summary>"from Rome" / "to Rome": a place that is not one of ours is named, so the traveller sees which one was not understood.</summary>
    private static void ReadUnknownCities(string[] tokens, bool[] used, bool originMissing, bool destinationMissing, List<VoiceIssueDto> issues)
    {
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var isFrom = tokens[i] == "from";
            var isTo = tokens[i] == "to";
            if (!(isFrom && originMissing || isTo && destinationMissing))
            {
                continue;
            }

            var next = tokens[i + 1];
            if (used[i + 1] || next.Length < 3 || next.All(char.IsDigit) || IsDateWord(next) || next is "the" or "my" or "now" or "any" or "here" or "fly" or "go" or "travel" or "book" or "see" or "find" or "search" or "get" or "know" or "check" or "show" or "look" or "leave" or "be" or "visit" or "take" or "buy")
            {
                continue;
            }

            issues.Add(new VoiceIssueDto(isFrom ? "unknown_origin" : "unknown_destination", next));
            used[i + 1] = true;
            return;
        }
    }

    private static bool IsDateWord(string word)
        => VoiceLexicon.Months.Any(e => e.Matches(word)) || VoiceLexicon.Weekdays.Any(e => e.Matches(word))
           || word is "today" or "tomorrow" or "tonight" or "next" or "this" or "morning" or "afternoon" or "evening" or "night";

    // ---- passengers, cabin, cheapest, time of day ----

    private static (int Count, bool Specified) ReadPassengers(string[] tokens, bool[] used, Dictionary<string, int> votes, List<VoiceIssueDto> issues)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i] || !TryNumber(tokens[i], out var count, out var lang))
            {
                continue;
            }

            var nounNext = i + 1 < tokens.Length && !used[i + 1] && VoiceLexicon.PassengerNouns.FirstOrDefault(n => n.Matches(tokens[i + 1])) is not null;
            var forNumber = i > 0 && !used[i - 1] && tokens[i - 1] is "for" or "на" or "for" && !(i + 1 < tokens.Length && IsMonth(tokens[i + 1]));
            var russianPlural = i > 0 && tokens[i - 1] == "на" && lang == "ru";
            if (!nounNext && !(forNumber && (lang == "en" || lang == null || russianPlural)))
            {
                continue;
            }

            used[i] = true;
            if (nounNext)
            {
                used[i + 1] = true;
            }

            if (forNumber)
            {
                used[i - 1] = true;
            }

            if (lang is not null)
            {
                votes[lang]++;
            }

            if (count is < 1 or > 9)
            {
                issues.Add(new VoiceIssueDto("bad_passengers", count.ToString(CultureInfo.InvariantCulture)));
                return (1, false);
            }

            return (count, true);
        }

        return (1, false);
    }

    private static bool TryNumber(string token, out int value, out string? language)
    {
        language = null;
        if (token.All(char.IsDigit) && token.Length is >= 1 and <= 3 && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        var entry = VoiceLexicon.Numbers.FirstOrDefault(n => n.Matches(token));
        value = entry?.Value ?? 0;
        language = entry?.Language;
        return entry is not null;
    }

    private static bool IsMonth(string token) => VoiceLexicon.Months.Any(m => m.Matches(token));

    private static T? ReadFirst<T>(VoiceLexicon.Entry<T>[] entries, string[] tokens, bool[] used, Dictionary<string, int> votes) where T : struct
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            var entry = entries.FirstOrDefault(e => e.Matches(tokens[i]));
            if (entry is not null)
            {
                used[i] = true;
                votes[entry.Language]++;
                return entry.Value;
            }
        }

        return default;
    }

    // ---- dates ----

    private sealed record When(DateOnly? Date, DateOnly? From, DateOnly? To, DepartureTimeOfDay? TimeOfDay);

    private sealed record NumericDate(DateOnly? Date, bool Ambiguous, bool Invalid, bool Past);

    private List<NumericDate> ExtractNumericDates(ref string raw, string? language, List<VoiceIssueDto> issues)
    {
        var found = new List<NumericDate>();
        var text = raw;

        text = IsoDate.Replace(text, match =>
        {
            found.Add(MakeDate(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), false));
            return " ";
        });

        text = NumericDatePattern.Replace(text, match =>
        {
            var first = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var second = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var separator = match.Groups[2].Value;
            var hasYear = match.Groups[4].Success;
            int year = hasYear ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;

            // 14.11 and 14/11 can only be day first. 05/11 is either: Azerbaijani and Russian read it day first, English asks.
            var ambiguous = separator == "/" && first <= 12 && second <= 12 && first != second && language?.ToLowerInvariant() == "en";
            if (ambiguous)
            {
                found.Add(new NumericDate(null, true, false, false));
                return " ";
            }

            var monthFirst = separator == "/" && first <= 12 && second > 12;
            var day = monthFirst ? second : first;
            var month = monthFirst ? first : second;
            found.Add(MakeDate(hasYear ? year : 0, month, day, !hasYear));
            return " ";
        });

        raw = text;
        return found;
    }

    private static NumericDate MakeDate(int year, int month, int day, bool noYear)
    {
        if (noYear)
        {
            // Resolved against today later: the next time this day comes round.
            return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2024, month)
                ? new NumericDate(new DateOnly(0001, month, day), false, false, false)
                : new NumericDate(null, false, true, false);
        }

        try
        {
            return new NumericDate(new DateOnly(year, month, day), false, false, false);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new NumericDate(null, false, true, false);
        }
    }

    private When ReadWhen(
        string[] tokens, bool[] used, Dictionary<string, int> votes, DateOnly today, string lang, List<NumericDate> numeric,
        List<VoiceIssueDto> issues, bool cheapest)
    {
        // 1. A date written with digits.
        foreach (var n in numeric)
        {
            if (n.Ambiguous)
            {
                issues.Add(new VoiceIssueDto("ambiguous_date"));
                return new When(null, null, null, null);
            }

            if (n.Invalid || n.Date is null)
            {
                issues.Add(new VoiceIssueDto("invalid_date"));
                return new When(null, null, null, null);
            }

            return Checked(NextOccurrence(n.Date.Value, today), today, issues);
        }

        // 2. A month name with a day ("November 15", "15 noyabr", "15 ноября"), or the month alone.
        for (var i = 0; i < tokens.Length; i++)
        {
            var month = used[i] ? null : VoiceLexicon.Months.FirstOrDefault(m => m.Matches(tokens[i]));
            if (month is null)
            {
                continue;
            }

            votes[month.Language]++;
            used[i] = true;
            var day = TakeDay(tokens, used, i);
            var year = TakeYear(tokens, used, i);
            if (day is null)
            {
                return MonthPeriod(month.Value, year, today, issues);
            }

            if (day is < 1 or > 31 || day > DateTime.DaysInMonth(year ?? 2024, month.Value))
            {
                issues.Add(new VoiceIssueDto("invalid_date"));
                return new When(null, null, null, null);
            }

            var date = year is { } y ? new DateOnly(y, month.Value, day.Value) : NextOccurrence(new DateOnly(0001, month.Value, day.Value), today);
            return Checked(date, today, issues);
        }

        // 3. Today, tomorrow, the day after, tonight.
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            var token = tokens[i];
            var next = i + 1 < tokens.Length ? tokens[i + 1] : string.Empty;
            (DateOnly? Day, string? Language, int Words, bool Tonight) hit = token switch
            {
                "today" => (today, "en", 1, false),
                "tomorrow" => (today.AddDays(1), "en", 1, false),
                "tonight" => (today, "en", 1, true),
                "sabah" => (today.AddDays(1), "az", 1, false),
                "bugun" => (today, "az", 1, false),
                "завтра" => (today.AddDays(1), "ru", 1, false),
                "послезавтра" => (today.AddDays(2), "ru", 1, false),
                "сегодня" => (today, "ru", 1, false),
                "bu" when next == "gun" => (today, "az", 2, false),
                "birisi" when next == "gun" => (today.AddDays(2), "az", 2, false),
                "day" when next == "after" && i + 2 < tokens.Length && tokens[i + 2] == "tomorrow" => (today.AddDays(2), "en", 3, false),
                _ => (null, null, 0, false)
            };

            if (hit.Day is { } day)
            {
                for (var k = 0; k < hit.Words; k++)
                {
                    used[i + k] = true;
                }

                votes[hit.Language!]++;
                return Checked(day, today, issues) with { TimeOfDay = hit.Tonight ? DepartureTimeOfDay.Evening : null };
            }
        }

        // 4. A weekday: the next one that comes round.
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            DayOfWeek? weekday = null;
            var consumed = 1;
            string? language = null;
            if (i + 1 < tokens.Length)
            {
                foreach (var (first, second, d) in VoiceLexicon.AzerbaijaniWeekdayPhrases)
                {
                    if (tokens[i] == VoiceText.Fold(first).Trim() && tokens[i + 1] == VoiceText.Fold(second).Trim())
                    {
                        weekday = d;
                        consumed = 2;
                        language = "az";
                    }
                }
            }

            if (weekday is null)
            {
                var entry = VoiceLexicon.Weekdays.Concat(VoiceLexicon.AzerbaijaniWeekdays).FirstOrDefault(e => e.Matches(tokens[i]));
                weekday = entry?.Value;
                language = entry?.Language;
            }

            if (weekday is { } wd)
            {
                for (var k = 0; k < consumed; k++)
                {
                    used[i + k] = true;
                }

                votes[language!]++;
                var ahead = ((int)wd - (int)today.DayOfWeek + 7) % 7;
                return Checked(today.AddDays(ahead == 0 ? 7 : ahead), today, issues);
            }
        }

        // 5. A period: the next N days, this or next week, this or next month.
        return ReadPeriod(tokens, used, votes, today);
    }

    private When ReadPeriod(string[] tokens, bool[] used, Dictionary<string, int> votes, DateOnly today)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            // "next 30 days", "gələn 30 gün", "ближайшие 30 дней"
            if (TryNumber(tokens[i], out var days, out _) && days is >= 2 and <= 366 && tokens[i].All(char.IsDigit)
                && i + 1 < tokens.Length && !used[i + 1] && DayWords.Contains(tokens[i + 1]))
            {
                used[i] = used[i + 1] = true;
                var language = tokens[i + 1].StartsWith("day", StringComparison.Ordinal) ? "en" : tokens[i + 1].StartsWith("gun", StringComparison.Ordinal) ? "az" : "ru";
                votes[language]++;
                for (var k = Math.Max(0, i - 2); k < i; k++)
                {
                    if (tokens[k] is "next" or "within" or "coming" or "gelen" or "novbeti" or "in" or "over" || tokens[k].StartsWith("ближайш", StringComparison.Ordinal)
                        || tokens[k].StartsWith("следующ", StringComparison.Ordinal) || tokens[k] == "течение")
                    {
                        used[k] = true;
                    }
                }

                return new When(null, today, today.AddDays(days - 1), null);
            }
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            var next = i + 1 < tokens.Length ? tokens[i + 1] : string.Empty;
            var isNext = tokens[i] is "next" or "gelen" or "novbeti" || tokens[i].StartsWith("следующ", StringComparison.Ordinal);
            var isThis = tokens[i] is "this" or "bu" || tokens[i].StartsWith("этом", StringComparison.Ordinal) || tokens[i].StartsWith("этои", StringComparison.Ordinal);
            var week = next.StartsWith("week", StringComparison.Ordinal) || next.StartsWith("hefte", StringComparison.Ordinal) || next.StartsWith("недел", StringComparison.Ordinal);
            var month = next == "month" || next == "ay" || next.StartsWith("месяц", StringComparison.Ordinal);
            if (!(isNext || isThis) || !(week || month))
            {
                continue;
            }

            used[i] = used[i + 1] = true;
            votes[next.StartsWith("hefte", StringComparison.Ordinal) || next == "ay" ? "az" : next.StartsWith("недел", StringComparison.Ordinal) || next.StartsWith("месяц", StringComparison.Ordinal) ? "ru" : "en"]++;
            if (week)
            {
                var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                var start = isNext ? monday.AddDays(7) : today;
                return new When(null, start, monday.AddDays(isNext ? 13 : 6), null);
            }

            if (isNext)
            {
                var first = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
                return new When(null, first, first.AddMonths(1).AddDays(-1), null);
            }

            return new When(null, today, new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1), null);
        }

        return new When(null, null, null, null);
    }

    private static When MonthPeriod(int month, int? year, DateOnly today, List<VoiceIssueDto> issues)
    {
        var y = year ?? (month < today.Month ? today.Year + 1 : today.Year);
        var first = new DateOnly(y, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        if (last < today)
        {
            issues.Add(new VoiceIssueDto("past_date", first.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
            return new When(null, null, null, null);
        }

        return new When(null, first < today ? today : first, last, null);
    }

    private static int? TakeDay(string[] tokens, bool[] used, int monthIndex)
    {
        // Looks right of the month first ("November 15", "noyabrın 15-i"), then left ("15 November", "15-ci noyabr", "15th of November").
        foreach (var step in new[] { 1, -1 })
        {
            var i = monthIndex + step;
            while (i >= 0 && i < tokens.Length && !used[i] && (Ordinals.Contains(tokens[i]) || tokens[i] is "of" or "the"))
            {
                i += step;
            }

            if (i >= 0 && i < tokens.Length && !used[i] && DayToken.Match(tokens[i]) is { Success: true } match)
            {
                for (var k = Math.Min(i, monthIndex); k <= Math.Max(i, monthIndex); k++)
                {
                    used[k] = true;
                }

                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static int? TakeYear(string[] tokens, bool[] used, int monthIndex)
    {
        for (var i = Math.Max(0, monthIndex - 3); i < Math.Min(tokens.Length, monthIndex + 4); i++)
        {
            if (!used[i] && YearToken.Match(tokens[i]) is { Success: true } match)
            {
                used[i] = true;
                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary>A day without a year (year 1) is the next time it comes round, this year or the next.</summary>
    private static DateOnly NextOccurrence(DateOnly date, DateOnly today)
    {
        if (date.Year != 1)
        {
            return date;
        }

        var thisYear = new DateOnly(today.Year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(today.Year, date.Month)));
        if (thisYear >= today)
        {
            return thisYear;
        }

        return new DateOnly(today.Year + 1, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(today.Year + 1, date.Month)));
    }

    private static When Checked(DateOnly date, DateOnly today, List<VoiceIssueDto> issues)
    {
        if (date < today)
        {
            issues.Add(new VoiceIssueDto("past_date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            return new When(null, null, null, null);
        }

        return new When(date, null, null, null);
    }
}
