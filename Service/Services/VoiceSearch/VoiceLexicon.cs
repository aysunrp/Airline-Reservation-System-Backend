using Domain.Enums;
using Service.Helpers.DTOs.Flight;

namespace Service.Services.VoiceSearch;

/// <summary>
/// The words of Azerbaijani, English and Russian that a flight request is made of. Every entry is written in its normal spelling and
/// folded once (see <see cref="VoiceText.Fold"/>), so "çərşənbə" and "cersenbe" are the same word. A stem ends with a star and
/// matches any word that starts with it.
/// </summary>
internal static class VoiceLexicon
{
    public sealed record Entry<T>(string Pattern, T Value, string Language)
    {
        public bool IsStem { get; } = Pattern.EndsWith('*');
        public string Folded { get; } = VoiceText.Fold(Pattern.TrimEnd('*')).Trim();

        public bool Matches(string word) => IsStem ? word.StartsWith(Folded, StringComparison.Ordinal) : word == Folded;
    }

    private static Entry<T> E<T>(string pattern, T value, string language) => new(pattern, value, language);

    /// <summary>What a word at the end of a city name may add (case endings, Russian declension). Anything longer is another word.</summary>
    public static readonly HashSet<string> CitySuffixes = new(new[]
    {
        "", "a", "e", "ya", "ye", "na", "ne", "da", "de", "nda", "nde", "dan", "den", "tan", "ten", "ndan", "nden", "in", "nin", "un", "nun",
        "i", "ni", "si", "s", "y", "u", "o", "ы", "а", "у", "е", "ю", "я", "и", "ом", "ем", "ой", "ей", "ою", "ы"
    }.Select(VoiceText.Fold));

    /// <summary>Endings that say "from this city" (Azerbaijani ablative). The prefixes below say it in English and Russian.</summary>
    public static readonly HashSet<string> OriginSuffixes = new(new[] { "dan", "den", "tan", "ten", "ndan", "nden" });

    /// <summary>Endings that say "to this city" (Azerbaijani dative).</summary>
    public static readonly HashSet<string> DestinationSuffixes = new(new[] { "a", "e", "ya", "ye", "na", "ne" });

    public static readonly HashSet<string> OriginMarkers = new(new[] { "from", "leaving", "departing", "из", "от", "со", "с" }.Select(VoiceText.Fold));
    public static readonly HashSet<string> DestinationMarkers = new(new[] { "to", "into", "towards", "в", "во", "до", "на" }.Select(VoiceText.Fold));

    /// <summary>City names as people say them; each airport is also known by its own city name. Keyed by IATA code.</summary>
    public static readonly Dictionary<string, string[]> CityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GYD"] = new[] { "Baku", "Bakı", "Баку" },
        ["DXB"] = new[] { "Dubai", "Dubay", "Дубай", "Дубаи" },
        ["IST"] = new[] { "Istanbul", "İstanbul", "Стамбул" },
        ["LHR"] = new[] { "London", "Londra", "Лондон" },
        ["NAJ"] = new[] { "Nakhchivan", "Naxçıvan", "Nakhichevan", "Нахичевань", "Нахчыван" },
        ["CDG"] = new[] { "Paris", "Париж" },
        ["TBS"] = new[] { "Tbilisi", "Tiflis", "Тбилиси" },
        ["VIE"] = new[] { "Vienna", "Vyana", "Wien", "Вена", "Вену", "Вены", "Вене" },
        ["SVO"] = new[] { "Moscow", "Moskva", "Москва", "Москву", "Москвы", "Москве" },
        ["LED"] = new[] { "Saint Petersburg", "Sankt-Peterburq", "Санкт-Петербург", "Питер" },
        ["BER"] = new[] { "Berlin", "Берлин" },
        ["FCO"] = new[] { "Rome", "Roma", "Рим" },
        ["MAD"] = new[] { "Madrid", "Мадрид" },
        ["AYT"] = new[] { "Antalya", "Анталья", "Анталия", "Анталию" },
        ["DOH"] = new[] { "Doha", "Доха" },
        ["CAI"] = new[] { "Cairo", "Qahirə", "Каир" },
        ["FRA"] = new[] { "Frankfurt", "Франкфурт" },
        ["AMS"] = new[] { "Amsterdam", "Амстердам" },
        ["JFK"] = new[] { "New York", "Nyu York", "Нью-Йорк", "Нью Йорк" },
        ["KBP"] = new[] { "Kyiv", "Kiev", "Kiyev", "Киев" },
        ["TLV"] = new[] { "Tel Aviv", "Тель-Авив" },
        ["SKD"] = new[] { "Samarkand", "Səmərqənd", "Самарканд" },
        ["TAS"] = new[] { "Tashkent", "Daşkənd", "Ташкент" }
    };

    public static readonly Entry<int>[] Numbers =
    {
        E("one", 1, "en"), E("two", 2, "en"), E("three", 3, "en"), E("four", 4, "en"), E("five", 5, "en"), E("six", 6, "en"),
        E("seven", 7, "en"), E("eight", 8, "en"), E("nine", 9, "en"),
        E("bir", 1, "az"), E("iki", 2, "az"), E("üç", 3, "az"), E("dörd", 4, "az"), E("beş", 5, "az"), E("altı", 6, "az"),
        E("yeddi", 7, "az"), E("səkkiz", 8, "az"), E("doqquz", 9, "az"),
        E("один", 1, "ru"), E("одного", 1, "ru"), E("два", 2, "ru"), E("две", 2, "ru"), E("двое", 2, "ru"), E("двоих", 2, "ru"), E("двух", 2, "ru"),
        E("три", 3, "ru"), E("трое", 3, "ru"), E("троих", 3, "ru"), E("трех", 3, "ru"), E("четыре", 4, "ru"), E("четверо", 4, "ru"),
        E("четырех", 4, "ru"), E("пять", 5, "ru"), E("пятерых", 5, "ru"), E("шесть", 6, "ru"), E("семь", 7, "ru"), E("восемь", 8, "ru"), E("девять", 9, "ru")
    };

    public static readonly Entry<bool>[] PassengerNouns =
    {
        E("passenger*", true, "en"), E("people", true, "en"), E("person*", true, "en"), E("adult*", true, "en"), E("traveler*", true, "en"),
        E("traveller*", true, "en"), E("seat*", true, "en"),
        E("nəfər*", true, "az"), E("sərnişin*", true, "az"), E("nefer*", true, "az"), E("adam*", true, "az"), E("böyük*", true, "az"),
        E("пассажир*", true, "ru"), E("человек*", true, "ru"), E("чел", true, "ru"), E("взросл*", true, "ru")
    };

    public static readonly Entry<CabinClass>[] Cabins =
    {
        E("business*", CabinClass.Business, "en"), E("biznes*", CabinClass.Business, "az"), E("бизнес*", CabinClass.Business, "ru"),
        E("economy*", CabinClass.Economy, "en"), E("ekonom*", CabinClass.Economy, "az"), E("эконом*", CabinClass.Economy, "ru"),
        E("comfort*", CabinClass.Comfort, "en"), E("komfort*", CabinClass.Comfort, "az"), E("комфорт*", CabinClass.Comfort, "ru")
    };

    public static readonly Entry<bool>[] Cheapest =
    {
        E("cheapest", true, "en"), E("cheap", true, "en"), E("lowest", true, "en"), E("cheaper", true, "en"), E("least expensive", true, "en"),
        E("ucuz*", true, "az"), E("sərfəli*", true, "az"),
        E("дешев*", true, "ru"), E("недорог*", true, "ru"), E("дёшев*", true, "ru")
    };

    /// <summary>Plural nouns (folded): with "cheapest" they ask for several low-priced options instead of the one cheapest flight.</summary>
    public static readonly HashSet<string> PluralWords = new(new[]
    {
        "flights", "tickets", "fares", "options", "deals", "uçuşlar", "uçuşları", "biletlər", "biletləri", "reyslər", "reysləri", "variantlar",
        "рейсы", "билеты", "варианты", "самые", "дешевые", "дешёвые", "недорогие"
    }.Select(VoiceText.Fold));

    public static readonly Entry<DepartureTimeOfDay>[] TimesOfDay =
    {
        E("morning", DepartureTimeOfDay.Morning, "en"), E("afternoon", DepartureTimeOfDay.Afternoon, "en"),
        E("evening", DepartureTimeOfDay.Evening, "en"), E("night", DepartureTimeOfDay.Night, "en"),
        E("səhər*", DepartureTimeOfDay.Morning, "az"), E("günorta*", DepartureTimeOfDay.Afternoon, "az"),
        E("axşam*", DepartureTimeOfDay.Evening, "az"), E("gecə*", DepartureTimeOfDay.Night, "az"),
        E("утр*", DepartureTimeOfDay.Morning, "ru"), E("днём", DepartureTimeOfDay.Afternoon, "ru"), E("вечер*", DepartureTimeOfDay.Evening, "ru"),
        E("ноч*", DepartureTimeOfDay.Night, "ru")
    };

    public static readonly Entry<int>[] Months =
    {
        E("january", 1, "en"), E("jan", 1, "en"), E("february", 2, "en"), E("feb", 2, "en"), E("march", 3, "en"), E("april", 4, "en"),
        E("may", 5, "en"), E("june", 6, "en"), E("july", 7, "en"), E("august", 8, "en"), E("aug", 8, "en"), E("september", 9, "en"),
        E("sept", 9, "en"), E("sep", 9, "en"), E("october", 10, "en"), E("oct", 10, "en"), E("november", 11, "en"), E("nov", 11, "en"),
        E("december", 12, "en"), E("dec", 12, "en"),
        E("yanvar*", 1, "az"), E("fevral*", 2, "az"), E("mart*", 3, "az"), E("aprel*", 4, "az"), E("may*", 5, "az"), E("iyun*", 6, "az"),
        E("iyul*", 7, "az"), E("avqust*", 8, "az"), E("sentyabr*", 9, "az"), E("oktyabr*", 10, "az"), E("noyabr*", 11, "az"), E("dekabr*", 12, "az"),
        E("январ*", 1, "ru"), E("феврал*", 2, "ru"), E("март*", 3, "ru"), E("апрел*", 4, "ru"), E("май", 5, "ru"), E("мая", 5, "ru"),
        E("мае", 5, "ru"), E("июн*", 6, "ru"), E("июл*", 7, "ru"), E("август*", 8, "ru"), E("сентябр*", 9, "ru"), E("октябр*", 10, "ru"),
        E("ноябр*", 11, "ru"), E("декабр*", 12, "ru")
    };

    /// <summary>Weekdays. The two-word Azerbaijani names are listed as phrases.</summary>
    public static readonly Entry<DayOfWeek>[] Weekdays =
    {
        E("monday", DayOfWeek.Monday, "en"), E("tuesday", DayOfWeek.Tuesday, "en"), E("wednesday", DayOfWeek.Wednesday, "en"),
        E("thursday", DayOfWeek.Thursday, "en"), E("friday", DayOfWeek.Friday, "en"), E("saturday", DayOfWeek.Saturday, "en"),
        E("sunday", DayOfWeek.Sunday, "en"),
        E("понедельник*", DayOfWeek.Monday, "ru"), E("вторник*", DayOfWeek.Tuesday, "ru"), E("сред*", DayOfWeek.Wednesday, "ru"),
        E("четверг*", DayOfWeek.Thursday, "ru"), E("пятниц*", DayOfWeek.Friday, "ru"), E("суббот*", DayOfWeek.Saturday, "ru"),
        E("воскресен*", DayOfWeek.Sunday, "ru")
    };

    /// <summary>Azerbaijani weekdays that are two words ("çərşənbə axşamı" is Tuesday, "cümə axşamı" Thursday, "bazar ertəsi" Monday).</summary>
    public static readonly (string First, string Second, DayOfWeek Day)[] AzerbaijaniWeekdayPhrases =
    {
        ("bazar", "ertəsi", DayOfWeek.Monday), ("çərşənbə", "axşamı", DayOfWeek.Tuesday), ("cümə", "axşamı", DayOfWeek.Thursday)
    };

    public static readonly Entry<DayOfWeek>[] AzerbaijaniWeekdays =
    {
        E("çərşənbə*", DayOfWeek.Wednesday, "az"), E("cümə*", DayOfWeek.Friday, "az"), E("şənbə*", DayOfWeek.Saturday, "az"), E("bazar", DayOfWeek.Sunday, "az")
    };

    public static readonly string[] Ordinals = { "st", "nd", "rd", "th", "i", "si", "ci", "cu", "u", "nci", "ncu", "ци", "ый", "го", "е", "ое" };
}
