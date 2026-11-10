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

        // "iki böyük, bir uşaq, bir körpə" is counted by kind; a plain "iki nəfər" is read as before.
        var travellers = ReadTravellers(tokens, used, votes, issues);
        var (passengers, passengersSpecified) = travellers is { } people
            ? (people.Adults + people.Children + people.Infants, true)
            : ReadPassengers(tokens, used, votes, issues);
        var cabin = ReadFirst(VoiceLexicon.Cabins, tokens, used, votes);
        var cheapest = ReadFirst(VoiceLexicon.Cheapest, tokens, used, votes) is true;
        // "the cheapest flight" is one flight; "the cheapest flights" (a plural noun) keeps several low-priced options.
        var single = cheapest && !tokens.Any(VoiceLexicon.PluralWords.Contains);
        var fastest = ReadFastest(tokens, used, votes);
        var stops = ReadStops(tokens, used, votes);

        var today = TodayAt(origin, utcNow);
        var lang = PickLanguage(votes, language);

        // A return day ("15 noyabr qayıdış", "qayıdış 15 noyabr") is taken out before the departure day is read, so the two are not mixed up.
        var (returnMarker, markerBindsDate) = FindReturnMarker(tokens, used);
        var wantsReturn = returnMarker >= 0;
        DateOnly? returnDate = wantsReturn && markerBindsDate ? ReadReturnDate(tokens, used, votes, today, numericDates, returnMarker, issues) : null;
        var period = ReadWhen(tokens, used, votes, today, lang, numericDates, issues, cheapest);
        if (wantsReturn && markerBindsDate && returnDate is null && period.Date is { } departureDay)
        {
            returnDate = ReadReturnDay(tokens, used, returnMarker, departureDay, issues);
        }

        if (wantsReturn)
        {
            var dateProblem = issues.Any(i => i.Code is "past_date" or "invalid_date" or "ambiguous_date");
            if (returnDate is null && !dateProblem)
            {
                issues.Add(new VoiceIssueDto("missing_return_date"));
            }
            else if (returnDate is { } back)
            {
                if (period.Date is not { } outbound)
                {
                    issues.Add(new VoiceIssueDto("return_needs_departure_date"));
                    returnDate = null;
                }
                else if (back < outbound)
                {
                    issues.Add(new VoiceIssueDto("return_before_departure", back.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    returnDate = null;
                }
            }
        }

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

        return Build(origin, destination, date, from, to, timeOfDay, cabin, passengers, passengersSpecified, cheapest, single, defaultPeriod, lang, issues,
            fastest, stops, returnDate, travellers?.Adults ?? passengers, travellers?.Children ?? 0, travellers?.Infants ?? 0);
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
        int passengers, bool passengersSpecified, bool cheapest, bool cheapestSingle, bool defaultPeriod, string detected, List<VoiceIssueDto> issues,
        bool fastest = false, string? stops = null, DateOnly? returnDate = null, int adults = 0, int children = 0, int infants = 0)
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
            "rules", issues, fastest, stops, returnDate, adults, children, infants);
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
        ReadUnknownAzerbaijaniCities(tokens, words, used, originAirport is null, destinationAirport is null, origin, destination, issues);
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

                // The names the destination catalog keeps for the city: English, Azerbaijani, Russian and the extra spellings.
                if (airport.CityRecord is { } record)
                {
                    foreach (var name in new[] { record.Name, record.NameAz, record.NameRu }.Concat(DestinationCatalogService.SplitAliases(record.Aliases)))
                    {
                        spellings.Add(VoiceText.Fold(name).Trim());
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

    private static readonly string[] DativeEndings = { "ya", "ye", "na", "ne", "a", "e" };
    private static readonly string[] AblativeEndings = { "dan", "den", "tan", "ten" };

    /// <summary>
    /// "Atlantidaya" / "Atlantidadan": a place that is not one of ours is named, so the traveller sees which one was not understood.
    /// A word counts only when it carries a case ending, is not an everyday word, and is either written with a capital or stands right next
    /// to the city that was understood (right after the origin, right before the destination).
    /// </summary>
    private static void ReadUnknownAzerbaijaniCities(
        string[] tokens, List<(string Folded, string Original)> words, bool[] used, bool originMissing, bool destinationMissing,
        Mention? origin, Mention? destination, List<VoiceIssueDto> issues)
    {
        bool Candidate(int i, string[] endings, bool adjacent)
        {
            if (i < 0 || i >= tokens.Length || used[i])
            {
                return false;
            }

            var word = tokens[i];
            if (word.Length < 5 || IsKnownWord(word) || !endings.Any(e => word.EndsWith(e, StringComparison.Ordinal) && word.Length - e.Length >= 3))
            {
                return false;
            }

            return adjacent || char.IsUpper(words[i].Original[0]);
        }

        void Report(string code, string[] endings, int adjacentIndex)
        {
            var found = Candidate(adjacentIndex, endings, true) ? adjacentIndex : Enumerable.Range(0, tokens.Length).FirstOrDefault(i => Candidate(i, endings, false), -1);
            if (found < 0)
            {
                return;
            }

            issues.Add(new VoiceIssueDto(code, words[found].Original));
            used[found] = true;
        }

        if (destinationMissing && !issues.Any(i => i.Code is "unknown_destination" or "ambiguous_destination"))
        {
            Report("unknown_destination", DativeEndings, origin is null ? -1 : origin.Start + origin.Length);
        }

        if (originMissing && !issues.Any(i => i.Code is "unknown_origin" or "ambiguous_origin"))
        {
            Report("unknown_origin", AblativeEndings, destination is null ? -1 : destination.Start - 1);
        }
    }

    /// <summary>A word the rules know (a month, a part of the day, a number, a cabin...): never a place name.</summary>
    private static bool IsKnownWord(string word)
        => IsDateWord(word) || DayWords.Contains(word) || VoiceLexicon.NotCities.Contains(word) || VoiceLexicon.PluralWords.Contains(word)
           || VoiceLexicon.AzerbaijaniWeekdays.Any(e => e.Matches(word)) || VoiceLexicon.TimesOfDay.Any(e => e.Matches(word))
           || VoiceLexicon.Cabins.Any(e => e.Matches(word)) || VoiceLexicon.Cheapest.Any(e => e.Matches(word)) || VoiceLexicon.Fastest.Any(e => e.Matches(word))
           || VoiceLexicon.DirectWords.Any(e => e.Matches(word)) || VoiceLexicon.ConnectingWords.Any(e => e.Matches(word))
           || VoiceLexicon.ReturnWords.Any(e => e.Matches(word)) || VoiceLexicon.RoundTripWords.Any(e => e.Matches(word))
           || VoiceLexicon.PassengerNouns.Any(e => e.Matches(word)) || VoiceLexicon.AdultNouns.Any(e => e.Matches(word))
           || VoiceLexicon.ChildNouns.Any(e => e.Matches(word)) || VoiceLexicon.InfantNouns.Any(e => e.Matches(word))
           || VoiceLexicon.Numbers.Any(e => e.Matches(word)) || word is "sabah" or "bugun" or "gelen" or "novbeti";

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

    private sealed record Travellers(int Adults, int Children, int Infants);

    /// <summary>
    /// "iki böyük, bir uşaq, bir körpə": each kind is counted by itself. Read only when a child or an infant is named with its number; a request
    /// without them is left to <see cref="ReadPassengers"/>. A child or an infant without an adult, or more infants than adults, is asked about.
    /// </summary>
    private static Travellers? ReadTravellers(string[] tokens, bool[] used, Dictionary<string, int> votes, List<VoiceIssueDto> issues)
    {
        var found = new List<(int Index, char Kind, int Count)>();
        for (var i = 1; i < tokens.Length; i++)
        {
            if (used[i] || used[i - 1] || !TryNumber(tokens[i - 1], out var count, out _))
            {
                continue;
            }

            var kind = VoiceLexicon.ChildNouns.Any(n => n.Matches(tokens[i])) ? 'C'
                : VoiceLexicon.InfantNouns.Any(n => n.Matches(tokens[i])) ? 'I'
                : VoiceLexicon.AdultNouns.Any(n => n.Matches(tokens[i])) ? 'A'
                : ' ';
            if (kind != ' ')
            {
                found.Add((i, kind, count));
            }
        }

        if (!found.Any(f => f.Kind is 'C' or 'I'))
        {
            return null;
        }

        int adults = 0, children = 0, infants = 0;
        foreach (var (index, kind, count) in found)
        {
            used[index] = used[index - 1] = true;
            votes["az"]++;
            switch (kind)
            {
                case 'A': adults += count; break;
                case 'C': children += count; break;
                default: infants += count; break;
            }
        }

        var total = adults + children + infants;
        if (total > 9)
        {
            issues.Add(new VoiceIssueDto("bad_passengers", total.ToString(CultureInfo.InvariantCulture)));
        }
        else if (adults == 0)
        {
            issues.Add(new VoiceIssueDto("missing_adult"));
        }
        else if (infants > adults)
        {
            issues.Add(new VoiceIssueDto("too_many_infants", infants.ToString(CultureInfo.InvariantCulture)));
        }

        return new Travellers(adults, children, infants);
    }

    /// <summary>"ən sürətli", "ən qısa": the traveller wants the shortest flight.</summary>
    private static bool ReadFastest(string[] tokens, bool[] used, Dictionary<string, int> votes)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            if (VoiceLexicon.Fastest.Any(e => e.Matches(tokens[i])))
            {
                used[i] = true;
                votes["az"]++;
                return true;
            }

            if (tokens[i] == VoiceLexicon.ShortestWord && i > 0 && !used[i - 1] && tokens[i - 1] == VoiceLexicon.SuperlativeWord)
            {
                used[i] = used[i - 1] = true;
                votes["az"]++;
                return true;
            }
        }

        return false;
    }

    /// <summary>Direct ("birbaşa", "aktarmasız") or connecting ("transferli"); both named ("birbaşa və ya transferli") means no preference.</summary>
    private static string? ReadStops(string[] tokens, bool[] used, Dictionary<string, int> votes)
    {
        bool direct = false, connecting = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            if (VoiceLexicon.DirectWords.Any(e => e.Matches(tokens[i])))
            {
                used[i] = true;
                direct = true;
                votes["az"]++;
            }
            else if (VoiceLexicon.ConnectingWords.Any(e => e.Matches(tokens[i])))
            {
                used[i] = true;
                connecting = true;
                votes["az"]++;
            }
        }

        return direct == connecting ? null : direct ? VoiceSearchInterpretationDto.StopsDirect : VoiceSearchInterpretationDto.StopsConnecting;
    }

    // ---- the return day of a round trip ----

    /// <summary>
    /// The word that says "return" ("qayıdış", "qayıdıram", "geri dönüş"), or -1. A day next to it is the return day. "gedib-gəlmə" and
    /// "gediş-dönüş" only say that the trip is a round trip: they have no day of their own (the second value is false), so the return day has
    /// to be said separately. The word is taken.
    /// </summary>
    private static (int Index, bool BindsDate) FindReturnMarker(string[] tokens, bool[] used)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (used[i] || !VoiceLexicon.ReturnWords.Any(e => e.Matches(tokens[i])))
            {
                continue;
            }

            var descriptor = i > 0 && !used[i - 1] && tokens[i - 1] == "gedis" && tokens[i].StartsWith("donus", StringComparison.Ordinal);
            if (descriptor)
            {
                used[i - 1] = true;
            }

            used[i] = true;
            return (i, !descriptor);
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!used[i] && VoiceLexicon.RoundTripWords.Any(e => e.Matches(tokens[i])))
            {
                used[i] = true;
                return (i, false);
            }
        }

        return (-1, false);
    }

    /// <summary>
    /// The return day when it is written with a month ("15 noyabr qayıdış", "qayıdış 15 noyabr") or when two dates are written in digits (the
    /// later one is the return). A return day with the month left out ("15-də qayıdıram") is read after the departure day, see ReadReturnDay.
    /// </summary>
    private static DateOnly? ReadReturnDate(
        string[] tokens, bool[] used, Dictionary<string, int> votes, DateOnly today, List<NumericDate> numeric, int marker, List<VoiceIssueDto> issues)
    {
        if (numeric.Count >= 2 && numeric.All(n => !n.Ambiguous && !n.Invalid && n.Date is not null))
        {
            var days = numeric.Select(n => NextOccurrence(n.Date!.Value, today)).OrderBy(d => d).ToList();
            numeric.Clear();
            numeric.Add(new NumericDate(days[0], false, false, false));
            return days[^1];
        }

        foreach (var step in new[] { 1, -1 })
        {
            for (var distance = 1; distance <= 5; distance++)
            {
                var i = marker + step * distance;
                if (i < 0 || i >= tokens.Length || used[i])
                {
                    continue;
                }

                var month = VoiceLexicon.Months.FirstOrDefault(m => m.Matches(tokens[i]));
                if (month is null)
                {
                    continue;
                }

                // Tried on a copy: a month alone, or a day that belongs to the other date, must not use up any word.
                var scratch = (bool[])used.Clone();
                scratch[i] = true;
                var day = TakeDay(tokens, scratch, i);
                if (day is null)
                {
                    continue;
                }

                var year = TakeYear(tokens, scratch, i);
                if (day is < 1 or > 31 || day > DateTime.DaysInMonth(year ?? 2024, month.Value))
                {
                    issues.Add(new VoiceIssueDto("invalid_date"));
                    return null;
                }

                for (var k = 0; k < used.Length; k++)
                {
                    used[k] |= scratch[k];
                }

                votes[month.Language]++;
                return year is { } y ? new DateOnly(y, month.Value, day.Value) : NextOccurrence(new DateOnly(0001, month.Value, day.Value), today);
            }
        }

        return null;
    }

    /// <summary>"15-də qayıdıram": a day number next to the return word, in the month of the departure (the next month when it is an earlier day).</summary>
    private static DateOnly? ReadReturnDay(string[] tokens, bool[] used, int marker, DateOnly departure, List<VoiceIssueDto> issues)
    {
        foreach (var step in new[] { 1, -1 })
        {
            for (var distance = 1; distance <= 4; distance++)
            {
                var i = marker + step * distance;
                if (i < 0 || i >= tokens.Length || used[i] || DayToken.Match(tokens[i]) is not { Success: true } match)
                {
                    continue;
                }

                var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (day is < 1 or > 31)
                {
                    continue;
                }

                used[i] = true;
                // The ending that goes with the number ("15-də": "də") is part of it.
                if (i + 1 < tokens.Length && !used[i + 1] && tokens[i + 1].Length <= 3 && !tokens[i + 1].All(char.IsDigit))
                {
                    used[i + 1] = true;
                }

                var first = new DateOnly(departure.Year, departure.Month, 1);
                var month = day < departure.Day ? first.AddMonths(1) : first;
                if (day > DateTime.DaysInMonth(month.Year, month.Month))
                {
                    issues.Add(new VoiceIssueDto("invalid_date"));
                    return null;
                }

                return new DateOnly(month.Year, month.Month, day);
            }
        }

        return null;
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

        // 2b. "2 gün sonra", "iki həftə sonra": that many days (weeks) from today. One day, not a period.
        for (var i = 0; i + 2 < tokens.Length; i++)
        {
            if (used[i] || used[i + 1] || used[i + 2] || tokens[i + 2] != "sonra" || !TryNumber(tokens[i], out var amount, out _) || amount is < 1 or > 60)
            {
                continue;
            }

            var weeks = tokens[i + 1].StartsWith("hefte", StringComparison.Ordinal);
            if (tokens[i + 1] != "gun" && !weeks)
            {
                continue;
            }

            used[i] = used[i + 1] = used[i + 2] = true;
            votes["az"]++;
            return Checked(today.AddDays(weeks ? amount * 7 : amount), today, issues);
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
