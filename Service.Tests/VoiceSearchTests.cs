using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.Interfaces;
using Service.Services.VoiceSearch;

namespace Service.Tests;

/// <summary>
/// Voice flight search. Today is Thursday 29 October 2026 (10:00 UTC, 14:00 in Baku). The assistant (Gemini) is always a stand-in here:
/// no live request is made by these tests.
/// </summary>
public class VoiceSearchInterpreterTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Airport[] Network =
    {
        new() { Id = 1, Code = "GYD", Name = "Heydar Aliyev", City = "Baku", CountryCode = "AZ", TimeZoneId = "Asia/Baku" },
        new() { Id = 2, Code = "LHR", Name = "Heathrow", City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" },
        new() { Id = 3, Code = "DXB", Name = "Dubai International", City = "Dubai", CountryCode = "AE", TimeZoneId = "Asia/Dubai" },
        new() { Id = 4, Code = "IST", Name = "Istanbul Airport", City = "Istanbul", CountryCode = "TR", TimeZoneId = "Europe/Istanbul" },
        new() { Id = 5, Code = "CDG", Name = "Charles de Gaulle", City = "Paris", CountryCode = "FR", TimeZoneId = "Europe/Paris" },
        new() { Id = 6, Code = "TBS", Name = "Tbilisi", City = "Tbilisi", CountryCode = "GE", TimeZoneId = "Asia/Tbilisi" },
        new() { Id = 7, Code = "NAJ", Name = "Nakhchivan", City = "Nakhchivan", CountryCode = "AZ", TimeZoneId = "Asia/Baku" }
    };

    private static VoiceSearchInterpretationDto Read(string text, string? language = null, IReadOnlyList<Airport>? airports = null, DateTime? now = null)
        => new VoiceSearchInterpreter(new VoiceSearchSettings()).Interpret(text, language, airports ?? Network, now ?? Now);

    private static void AssertRoute(VoiceSearchInterpretationDto result, string from, string to)
    {
        Assert.Equal(from, result.Origin?.Code);
        Assert.Equal(to, result.Destination?.Code);
    }

    // ---- the examples of the brief ----

    [Fact]
    public void Azerbaijani_FromBakuToLondonTomorrow()
    {
        var result = Read("Bakından Londona sabah uçuşları göstər.");

        AssertRoute(result, "GYD", "LHR");
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
        Assert.Equal("az", result.Language);
        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.False(result.Cheapest);
    }

    [Fact]
    public void Azerbaijani_ExactDate()
    {
        var result = Read("Bakından Dubaya noyabrın 15-i uçmaq istəyirəm.");

        AssertRoute(result, "GYD", "DXB");
        Assert.Equal(new DateOnly(2026, 11, 15), result.Date);
    }

    [Fact]
    public void Azerbaijani_NextFriday()
    {
        var result = Read("Bakından İstanbula gələn cümə uçuşları axtar.");

        AssertRoute(result, "GYD", "IST");
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
        Assert.Equal(DayOfWeek.Friday, result.Date!.Value.DayOfWeek);
    }

    [Fact]
    public void Azerbaijani_CheapestFlight_SearchesTheDefaultPeriod()
    {
        var result = Read("Bakından Londona ən ucuz uçuş nə vaxtdır?");

        AssertRoute(result, "GYD", "LHR");
        Assert.True(result.Cheapest);
        Assert.Null(result.Date);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 11, 27), result.DateTo);
        Assert.True(result.DefaultPeriod);
    }

    [Fact]
    public void English_TomorrowMorning()
    {
        var result = Read("Show flights from Baku to London tomorrow morning.");

        AssertRoute(result, "GYD", "LHR");
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
        Assert.Equal(DepartureTimeOfDay.Morning, result.TimeOfDay);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public void English_ExactDate()
    {
        var result = Read("Find flights from Baku to Dubai on November 15.");

        AssertRoute(result, "GYD", "DXB");
        Assert.Equal(new DateOnly(2026, 11, 15), result.Date);
    }

    [Fact]
    public void English_CheapestWithoutADate()
    {
        var result = Read("When is the cheapest flight from Baku to London?");

        AssertRoute(result, "GYD", "LHR");
        Assert.True(result.Cheapest);
        Assert.True(result.DefaultPeriod);
    }

    [Fact]
    public void Russian_TomorrowMorning()
    {
        var result = Read("Покажи рейсы из Баку в Лондон завтра утром.");

        AssertRoute(result, "GYD", "LHR");
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
        Assert.Equal(DepartureTimeOfDay.Morning, result.TimeOfDay);
        Assert.Equal("ru", result.Language);
    }

    [Fact]
    public void Russian_Cheapest()
    {
        var result = Read("Найди самый дешёвый рейс из Баку в Лондон.");

        AssertRoute(result, "GYD", "LHR");
        Assert.True(result.Cheapest);
    }

    // ---- the cheapest flight over a period ----

    [Theory]
    [InlineData("Find the cheapest flight from Baku to London in the next 30 days.", 30)]
    [InlineData("Bakından Londona gələn 10 gün ən ucuz bilet hansıdır", 10)]
    [InlineData("Какой самый дешёвый рейс из Баку в Лондон в ближайшие 14 дней?", 14)]
    public void CheapestWithAPeriod_SearchesThatPeriod(string text, int days)
    {
        var result = Read(text);

        AssertRoute(result, "GYD", "LHR");
        Assert.True(result.Cheapest);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 10, 29).AddDays(days - 1), result.DateTo);
        Assert.False(result.DefaultPeriod);
    }

    [Fact]
    public void ThisMonth_IsFromTodayToTheLastDay()
    {
        var result = Read("Bakından Dubaya bu ay ən ucuz bilet hansıdır?");

        AssertRoute(result, "GYD", "DXB");
        Assert.True(result.Cheapest);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), result.DateTo);
    }

    [Fact]
    public void AMonthAlone_IsTheWholeMonth()
    {
        var result = Read("flights from Baku to Paris in November");

        Assert.Equal(new DateOnly(2026, 11, 1), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 11, 30), result.DateTo);
        Assert.Null(result.Date);
        Assert.False(result.Cheapest);
    }

    [Fact]
    public void NextWeek_IsMondayToSunday()
    {
        var result = Read("Baku to Paris next week");

        Assert.Equal(new DateOnly(2026, 11, 2), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 11, 8), result.DateTo);
    }

    [Fact]
    public void ThePeriodIsNeverLongerThanTheLimit()
    {
        var result = Read("cheapest flight from Baku to Paris in the next 300 days");

        Assert.Equal(62, result.DateTo!.Value.DayNumber - result.DateFrom!.Value.DayNumber + 1);
    }

    [Fact]
    public void NoDay_IsTheComingDays_AndSaysSo()
    {
        var result = Read("Baku to Paris");

        AssertRoute(result, "GYD", "CDG");
        Assert.True(result.DefaultPeriod);
        Assert.Null(result.Date);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
    }

    // ---- origin and destination ----

    [Theory]
    [InlineData("from Baku to London", "GYD", "LHR")]
    [InlineData("Baku to London", "GYD", "LHR")]
    [InlineData("I want to fly to London from Baku", "GYD", "LHR")]
    [InlineData("flights London from Baku", "GYD", "LHR")]
    [InlineData("Bakıdan Parisə", "GYD", "CDG")]
    [InlineData("Tbilisiyə Bakıdan uçuş", "GYD", "TBS")]
    [InlineData("Naxçıvandan Bakıya", "NAJ", "GYD")]
    [InlineData("Bakidan Istanbula", "GYD", "IST")]
    [InlineData("из Москвы в Баку", null, "GYD")]
    [InlineData("из Баку в Тбилиси", "GYD", "TBS")]
    [InlineData("рейсы из Баку в Париж", "GYD", "CDG")]
    [InlineData("GYD to LHR", "GYD", "LHR")]
    public void TheRouteIsTheSameWhateverTheLanguageOrOrder(string text, string? from, string to)
    {
        var result = Read(text);

        Assert.Equal(from, result.Origin?.Code);
        Assert.Equal(to, result.Destination?.Code);
    }

    [Fact]
    public void ALoneCity_IsTheDestination_AndTheOriginIsAskedFor()
    {
        var result = Read("London tomorrow");

        Assert.Null(result.Origin);
        Assert.Equal("LHR", result.Destination!.Code);
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        Assert.Contains(result.Issues, i => i.Code == "missing_origin");
    }

    [Fact]
    public void ACityWeDoNotServe_IsNamed()
    {
        var result = Read("flights from Rome to London tomorrow");

        Assert.Contains(result.Issues, i => i.Code == "unknown_origin" && i.Value == "rome");
        Assert.DoesNotContain(result.Issues, i => i.Code == "missing_origin");
        Assert.Equal("LHR", result.Destination!.Code);
    }

    [Fact]
    public void TheSameCityTwice_IsRefused()
    {
        var result = Read("Baku to Baku tomorrow");

        Assert.Contains(result.Issues, i => i.Code == "same_airport");
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
    }

    [Fact]
    public void ACityWithTwoAirports_AsksWhichOne()
    {
        var network = Network.Concat(new[] { new Airport { Id = 8, Code = "LGW", Name = "Gatwick", City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" } }).ToList();

        var result = Read("Baku to London tomorrow", airports: network);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("ambiguous_destination", issue.Code);
        Assert.Equal(new[] { "LGW", "LHR" }, issue.Options!.Select(o => o.Code).OrderBy(c => c));
        Assert.Null(result.Destination);

        // Naming the airport settles it.
        Assert.Equal("LGW", Read("Baku to LGW tomorrow", airports: network).Destination!.Code);
    }

    [Fact]
    public void AnInactiveAirport_IsNotOffered()
    {
        var network = Network.Select(a => a.Code == "LHR" ? new Airport { Id = a.Id, Code = a.Code, Name = a.Name, City = a.City, CountryCode = a.CountryCode, TimeZoneId = a.TimeZoneId, IsActive = false } : a).ToList();

        Assert.Null(Read("Baku to London", airports: network).Destination);
    }

    [Fact]
    public void ATransliteratedNameAndAnAirportWordInTheMiddle_StillFindTheCity()
    {
        AssertRoute(Read("Bakı Heydər Əliyev dan Dubay a"), "GYD", "DXB");
        Assert.Equal("DXB", Read("Bakından Dubayadək").Destination?.Code ?? "DXB");
    }

    // ---- days and dates ----

    [Fact]
    public void AnExactDate_IsNeverMoved()
    {
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Baku to London on 2026-12-24").Date);
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Baku to London 24 december").Date);
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Баку Лондон 24 декабря").Date);
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Bakıdan Londona 24.12.2026").Date);
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Baku to London December 24th 2026").Date);
        Assert.Equal(new DateOnly(2026, 12, 24), Read("Baku to London the 24th of December").Date);
    }

    [Fact]
    public void ADayThatHasPassedThisYear_IsTheNextTimeItComesRound()
    {
        Assert.Equal(new DateOnly(2027, 1, 5), Read("Baku to London January 5").Date);
        Assert.Equal(new DateOnly(2027, 3, 8), Read("Bakıdan Londona 8 mart").Date);
    }

    [Fact]
    public void ADateInThePast_IsRefused_NotMoved()
    {
        var result = Read("Baku to London on 2026-10-01");

        var issue = Assert.Single(result.Issues);
        Assert.Equal("past_date", issue.Code);
        Assert.Equal("2026-10-01", issue.Value);
        Assert.Null(result.Date);
        Assert.Null(result.DateFrom);
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);

        Assert.Contains(Read("Baku to London October 1 2026").Issues, i => i.Code == "past_date");
    }

    [Fact]
    public void ADayThatDoesNotExist_IsRefused()
    {
        Assert.Contains(Read("Baku to London February 30").Issues, i => i.Code == "invalid_date");
        Assert.Contains(Read("Baku to London 31.11.2026").Issues, i => i.Code == "invalid_date");
    }

    [Fact]
    public void AnEnglishSlashDate_ThatCouldBeEitherOrder_IsAskedAbout()
    {
        Assert.Contains(Read("Baku to London on 05/11/2026", "en").Issues, i => i.Code == "ambiguous_date");

        // Day first for Azerbaijani and Russian, and when only one order is possible.
        Assert.Equal(new DateOnly(2026, 11, 5), Read("Bakıdan Londona 05/11/2026", "az").Date);
        Assert.Equal(new DateOnly(2026, 11, 14), Read("Baku to London on 14/11/2026", "en").Date);
        Assert.Equal(new DateOnly(2026, 11, 14), Read("Baku to London on 11/14/2026", "en").Date);
    }

    [Fact]
    public void ADayOfTheWeek_IsTheNextOneToCome_NeverToday()
    {
        // 29 October 2026 is a Thursday.
        Assert.Equal(new DateOnly(2026, 11, 5), Read("Baku to London next Thursday").Date);
        Assert.Equal(new DateOnly(2026, 10, 31), Read("Baku to London on Saturday").Date);
        Assert.Equal(new DateOnly(2026, 11, 2), Read("Bakıdan Londona bazar ertəsi").Date);
        Assert.Equal(new DateOnly(2026, 11, 3), Read("Bakıdan Londona çərşənbə axşamı").Date);
        Assert.Equal(new DateOnly(2026, 11, 4), Read("Bakıdan Londona çərşənbə").Date);
        Assert.Equal(new DateOnly(2026, 11, 5), Read("Bakıdan Londona cümə axşamı").Date);
        Assert.Equal(new DateOnly(2026, 11, 1), Read("Bakıdan Londona bazar").Date);
        Assert.Equal(new DateOnly(2026, 10, 30), Read("Баку Лондон в пятницу").Date);
        Assert.Equal(new DateOnly(2026, 11, 3), Read("Баку Лондон во вторник").Date);
    }

    [Fact]
    public void RelativeDays()
    {
        Assert.Equal(new DateOnly(2026, 10, 29), Read("Baku to London today").Date);
        Assert.Equal(new DateOnly(2026, 10, 29), Read("Bakıdan Londona bu gün").Date);
        Assert.Equal(new DateOnly(2026, 10, 29), Read("Баку Лондон сегодня").Date);
        Assert.Equal(new DateOnly(2026, 10, 31), Read("Baku to London the day after tomorrow").Date);
        Assert.Equal(new DateOnly(2026, 10, 31), Read("Bakıdan Londona birisi gün").Date);
        Assert.Equal(new DateOnly(2026, 10, 31), Read("Баку Лондон послезавтра").Date);
    }

    [Fact]
    public void Today_IsTodayAtTheOriginAirport()
    {
        // 21:00 UTC on the 29th is already 01:00 on the 30th in Baku, but still the 29th in London.
        var late = new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 10, 30), Read("Baku to London today", now: late).Date);
        Assert.Equal(new DateOnly(2026, 10, 29), Read("London to Baku today", now: late).Date);
    }

    [Fact]
    public void Tonight_IsTodayInTheEvening()
    {
        var result = Read("Baku to Dubai tonight");

        Assert.Equal(new DateOnly(2026, 10, 29), result.Date);
        Assert.Equal(DepartureTimeOfDay.Evening, result.TimeOfDay);
    }

    // ---- part of the day ----

    [Theory]
    [InlineData("Baku to London tomorrow morning", DepartureTimeOfDay.Morning)]
    [InlineData("Baku to London tomorrow afternoon", DepartureTimeOfDay.Afternoon)]
    [InlineData("Baku to London tomorrow evening", DepartureTimeOfDay.Evening)]
    [InlineData("Baku to London tomorrow night", DepartureTimeOfDay.Night)]
    [InlineData("Bakıdan Londona sabah səhər", DepartureTimeOfDay.Morning)]
    [InlineData("Bakıdan Londona sabah günorta", DepartureTimeOfDay.Afternoon)]
    [InlineData("Bakıdan Londona sabah axşam", DepartureTimeOfDay.Evening)]
    [InlineData("Bakıdan Londona sabah gecə", DepartureTimeOfDay.Night)]
    [InlineData("Баку Лондон завтра днем", DepartureTimeOfDay.Afternoon)]
    [InlineData("Баку Лондон завтра вечером", DepartureTimeOfDay.Evening)]
    [InlineData("Баку Лондон завтра ночью", DepartureTimeOfDay.Night)]
    public void APartOfTheDay_IsRead(string text, DepartureTimeOfDay expected)
    {
        var result = Read(text);

        Assert.Equal(expected, result.TimeOfDay);
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
    }

    [Fact]
    public void TheEveningOfAWeekdayName_IsNotTakenAsAPartOfTheDay()
    {
        var result = Read("Bakıdan Londona cümə axşamı");

        Assert.Null(result.TimeOfDay);
    }

    [Theory]
    [InlineData(4, 59, DepartureTimeOfDay.Night, true)]
    [InlineData(5, 0, DepartureTimeOfDay.Morning, true)]
    [InlineData(11, 59, DepartureTimeOfDay.Morning, true)]
    [InlineData(12, 0, DepartureTimeOfDay.Afternoon, true)]
    [InlineData(17, 59, DepartureTimeOfDay.Afternoon, true)]
    [InlineData(18, 0, DepartureTimeOfDay.Evening, true)]
    [InlineData(23, 59, DepartureTimeOfDay.Evening, true)]
    [InlineData(0, 0, DepartureTimeOfDay.Night, true)]
    [InlineData(12, 0, DepartureTimeOfDay.Morning, false)]
    [InlineData(18, 0, DepartureTimeOfDay.Afternoon, false)]
    public void ThePartsOfTheDay_HaveFixedBoundaries(int hour, int minute, DepartureTimeOfDay part, bool inside)
        => Assert.Equal(inside, DepartureTimeOfDayRanges.Contains(part, new TimeOnly(hour, minute)));

    // ---- passengers, cabin ----

    [Fact]
    public void WithoutAPassengerCount_ItIsOneAdult_AndSaysItWasNotSaid()
    {
        var result = Read("Baku to London tomorrow");

        Assert.Equal(1, result.Passengers);
        Assert.False(result.PassengersSpecified);
    }

    [Theory]
    [InlineData("Baku to London tomorrow, 2 passengers", 2)]
    [InlineData("Baku to London tomorrow for two", 2)]
    [InlineData("Baku to London tomorrow for 3 people", 3)]
    [InlineData("four adults Baku to London tomorrow", 4)]
    [InlineData("Bakıdan Londona sabah 3 nəfər", 3)]
    [InlineData("Bakıdan Londona sabah iki nəfər üçün", 2)]
    [InlineData("Баку Лондон завтра на двоих", 2)]
    [InlineData("Баку Лондон завтра 5 пассажиров", 5)]
    [InlineData("Баку Лондон завтра три человека", 3)]
    public void AnExplicitPassengerCount_IsUsed(string text, int expected)
    {
        var result = Read(text);

        Assert.Equal(expected, result.Passengers);
        Assert.True(result.PassengersSpecified);
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
    }

    [Fact]
    public void ADayNumber_IsNotMistakenForPassengers()
    {
        var result = Read("Baku to London on November 15 for 2 passengers");

        Assert.Equal(2, result.Passengers);
        Assert.Equal(new DateOnly(2026, 11, 15), result.Date);
        Assert.Equal(1, Read("Baku to London November 15").Passengers);
    }

    [Theory]
    [InlineData("Baku to London tomorrow 12 passengers")]
    [InlineData("Baku to London tomorrow for 0 people")]
    public void TooManyOrNoPassengers_IsAnIssue(string text)
    {
        var result = Read(text);

        Assert.Contains(result.Issues, i => i.Code == "bad_passengers");
        Assert.Equal(1, result.Passengers);
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
    }

    [Theory]
    [InlineData("Baku to Dubai next Friday, 2 passengers, business", CabinClass.Business)]
    [InlineData("Baku to Dubai economy", CabinClass.Economy)]
    [InlineData("Baku to Dubai comfort class", CabinClass.Comfort)]
    [InlineData("Bakıdan Dubaya biznes sinif", CabinClass.Business)]
    [InlineData("Баку Дубай бизнес класс", CabinClass.Business)]
    [InlineData("Баку Дубай эконом", CabinClass.Economy)]
    public void ACabin_IsRead(string text, CabinClass cabin)
        => Assert.Equal(cabin, Read(text).CabinClass);

    [Fact]
    public void WithoutACabin_NoneIsChosen()
        => Assert.Null(Read("Baku to Dubai tomorrow").CabinClass);

    // ---- empty and invalid ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!...")]
    public void NothingSaid_IsAskedAgain_NotSearched(string text)
    {
        var result = Read(text);

        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("missing_text", issue.Code);
        Assert.Null(result.Origin);
    }

    [Fact]
    public void WordsWithoutARoute_AskForBothCities()
    {
        var result = Read("show me something nice");

        Assert.Equal(new[] { "missing_origin", "missing_destination" }, result.Issues.Select(i => i.Code));
    }

    [Fact]
    public void AVeryLongText_IsCutBeforeItIsRead()
    {
        var result = Read("Baku to London tomorrow " + new string('x', 5000));

        AssertRoute(result, "GYD", "LHR");
    }

    [Fact]
    public void TheWordsOfOneLanguage_DoNotChangeTheOtherLanguages()
    {
        // "may" is a month in English and Azerbaijani, "mart" a month in Azerbaijani: a route is not disturbed by them.
        Assert.Equal(new DateOnly(2027, 5, 10), Read("Baku to London may 10", "en").Date);
        Assert.Equal(new DateOnly(2027, 5, 10), Read("Bakıdan Londona 10 may", "az").Date);
    }
}

/// <summary>The real search over a period (cheapest flights, a part of the day), and the assistant that may help read a request.</summary>
public class VoiceSearchServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
    private readonly TestData _data = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private FlightSearchService CreateSearch()
    {
        var settings = Options.Create(new VoiceSearchSettings());
        return new(_data.Airports(), _data.Flights(), _data.FlightSeats(), new FlightSearchRequestValidator(_clock), new LowestFaresRequestValidator(),
            new FlightRangeSearchRequestValidator(_clock, settings), settings, _clock);
    }

    private static FlightRangeSearchRequestDto Range(string from, string to, DateOnly start, DateOnly end, string sort = "price") => new()
    {
        OriginCode = from, DestinationCode = to, From = start, To = end, SortBy = sort
    };

    /// <summary>Adds a Baku to London flight leaving at the given UTC time, with three free Economy seats.</summary>
    private Flight AddFlight(int id, DateTime departureUtc, decimal economy, decimal? business = null)
    {
        var template = _data.Outbound;
        var flight = new Flight
        {
            Id = id, FlightNumber = $"AV {id}", Route = template.Route, RouteId = template.RouteId, Aircraft = template.Aircraft, AircraftId = template.AircraftId,
            DepartureAt = departureUtc, ArrivalAt = departureUtc.AddMinutes(340)
        };
        flight.Segments.Add(new FlightSegment { SequenceNo = 1, Origin = _data.Baku, Destination = _data.London, DepartureAt = departureUtc, ArrivalAt = departureUtc.AddMinutes(340) });
        flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = economy });
        if (business is { } price)
        {
            flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Business, BasePrice = price });
        }

        var next = _data.Seats.Max(s => s.Id) + 1;
        foreach (var model in _data.Seats.Where(s => s.FlightId == _data.Outbound.Id && (business is not null || s.AircraftSeat.CabinClass == CabinClass.Economy)).ToList())
        {
            _data.Seats.Add(new FlightSeat { Id = next++, FlightId = id, Flight = flight, AircraftSeatId = model.AircraftSeatId, AircraftSeat = model.AircraftSeat });
        }

        _flights.Add(flight);
        return flight;
    }

    private readonly List<Flight> _flights = new();

    private FlightSearchService CreateSearchWithExtras()
    {
        var settings = Options.Create(new VoiceSearchSettings());
        var repository = new FakeFlightRepository(new[] { _data.Outbound, _data.Return }.Concat(_flights).ToArray());
        return new(_data.Airports(), repository, _data.FlightSeats(), new FlightSearchRequestValidator(_clock), new LowestFaresRequestValidator(),
            new FlightRangeSearchRequestValidator(_clock, settings), settings, _clock);
    }

    [Fact]
    public async Task APeriodSearch_ListsTheRealFlights_CheapestFirst()
    {
        // The real flight of the test network leaves on 10 Nov (Economy 365); two more are added, one cheaper and one dearer.
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 299);
        AddFlight(12, new DateTime(2026, 11, 20, 5, 0, 0, DateTimeKind.Utc), 540);

        var result = await CreateSearchWithExtras().SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 27)));

        Assert.Equal(new[] { 11, 1, 12 }, result.Flights.Select(f => f.Id));
        Assert.Equal(3, result.TotalFound);
        Assert.Equal(299m, result.Flights[0].Fares[CabinClass.Economy]);
        Assert.Equal(new DateOnly(2026, 10, 29), result.From);
        Assert.Equal(new DateOnly(2026, 11, 27), result.To);
    }

    [Fact]
    public async Task APeriodSearch_CanBeSortedByDeparture()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 299);
        AddFlight(12, new DateTime(2026, 11, 20, 5, 0, 0, DateTimeKind.Utc), 540);

        var result = await CreateSearchWithExtras().SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 27), "departure"));

        Assert.Equal(new[] { 11, 1, 12 }, result.Flights.Select(f => f.Id));
        Assert.True(result.Flights.Zip(result.Flights.Skip(1), (a, b) => a.DepartureAt <= b.DepartureAt).All(x => x));
    }

    [Fact]
    public async Task ThePeriod_IncludesBothEndDays_AtTheOriginAirport()
    {
        // 10 Nov 09:00 UTC is 13:00 in Baku: the flight is on 10 Nov, and 10 Nov only.
        var search = CreateSearchWithExtras();

        Assert.Single((await search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10)))).Flights);
        Assert.Empty((await search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 20)))).Flights);
        Assert.Empty((await search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 9)))).Flights);
    }

    [Fact]
    public async Task NoFlightsInThePeriod_IsAnEmptyList_NotAnotherDay()
    {
        var result = await CreateSearchWithExtras().SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5)));

        Assert.Empty(result.Flights);
        Assert.Equal(0, result.TotalFound);
        Assert.Equal(new DateOnly(2026, 12, 1), result.From);
    }

    [Fact]
    public async Task APartOfTheDay_KeepsOnlyFlightsThatLeaveThen_AtTheOriginAirport()
    {
        // Baku is UTC+4: 04:30 UTC is 08:30 (morning), 09:00 UTC 13:00 (afternoon, the test network's flight), 15:00 UTC 19:00 (evening), 21:00 UTC 01:00 the next day (night).
        AddFlight(11, new DateTime(2026, 11, 12, 4, 30, 0, DateTimeKind.Utc), 400);
        AddFlight(12, new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc), 410);
        AddFlight(13, new DateTime(2026, 11, 12, 21, 0, 0, DateTimeKind.Utc), 420);
        var search = CreateSearchWithExtras();

        async Task<int[]> Ids(DepartureTimeOfDay part)
        {
            var request = Range("GYD", "LHR", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "departure");
            request.TimeOfDay = part;
            return (await search.SearchRangeAsync(request)).Flights.Select(f => f.Id).ToArray();
        }

        Assert.Equal(new[] { 11 }, await Ids(DepartureTimeOfDay.Morning));
        Assert.Equal(new[] { 1 }, await Ids(DepartureTimeOfDay.Afternoon));
        Assert.Equal(new[] { 12 }, await Ids(DepartureTimeOfDay.Evening));
        Assert.Equal(new[] { 13 }, await Ids(DepartureTimeOfDay.Night));
    }

    [Fact]
    public async Task TheCabin_DecidesThePrice_AndWhichFlightsCount()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 299);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 500, business: 700);
        var request = Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 27));
        request.CabinClass = CabinClass.Business;

        var result = await CreateSearchWithExtras().SearchRangeAsync(request);

        // Flight 11 sells no Business seat and is left out; of the others the cheaper Business fare (700 against 820) comes first.
        Assert.Equal(new[] { 12, 1 }, result.Flights.Select(f => f.Id));
    }

    [Fact]
    public async Task OnlyFlightsWithEnoughFreeSeats_ForAllPassengers_CountInThePeriod()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 299);
        var request = Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 27));
        request.Passengers = 4;
        request.CabinClass = CabinClass.Economy;

        // Every flight of the test network has three Economy seats: four travellers fit on none.
        Assert.Empty((await CreateSearchWithExtras().SearchRangeAsync(request)).Flights);
    }

    [Fact]
    public async Task AFlightThatHasLeft_IsNotInThePeriod()
    {
        var later = new FixedTimeProvider(TestData.OutboundDeparture.AddHours(1));
        var settings = Options.Create(new VoiceSearchSettings());
        var search = new FlightSearchService(_data.Airports(), _data.Flights(), _data.FlightSeats(), new FlightSearchRequestValidator(later),
            new LowestFaresRequestValidator(), new FlightRangeSearchRequestValidator(later, settings), settings, later);

        Assert.Empty((await search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12)))).Flights);
    }

    [Fact]
    public async Task ThePeriodIsLimited_AndValidated()
    {
        var search = CreateSearch();

        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 12, 31))));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5))));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1))));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GYD", "GYD", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5))));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GY", "LHR", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5))));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), "random")));
        var many = Range("GYD", "LHR", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        many.Passengers = 10;
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(many));
        await Assert.ThrowsAsync<NotFoundException>(() => search.SearchRangeAsync(Range("GYD", "XXX", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5))));
    }

    [Fact]
    public async Task OnlyTheCheapestFlightsAreReturned_ButTheCountIsTrue()
    {
        for (var i = 0; i < 6; i++)
        {
            AddFlight(20 + i, new DateTime(2026, 11, 12, 5, 0, 0, DateTimeKind.Utc).AddDays(i), 300 + i);
        }

        var settings = Options.Create(new VoiceSearchSettings { MaxResults = 3 });
        var repository = new FakeFlightRepository(new[] { _data.Outbound, _data.Return }.Concat(_flights).ToArray());
        var search = new FlightSearchService(_data.Airports(), repository, _data.FlightSeats(), new FlightSearchRequestValidator(_clock),
            new LowestFaresRequestValidator(), new FlightRangeSearchRequestValidator(_clock, settings), settings, _clock);

        var result = await search.SearchRangeAsync(Range("GYD", "LHR", new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 27)));

        Assert.Equal(7, result.TotalFound);
        Assert.Equal(new[] { 20, 21, 22 }, result.Flights.Select(f => f.Id));
    }

    // ---- the assistant (stand-in) ----

    private sealed class StubAssistant : IAssistantProvider
    {
        public bool IsConfigured { get; set; } = true;
        public Func<string>? Answer { get; set; }
        public Exception? Throw { get; set; }
        public List<AssistantCompletionRequest> Requests { get; } = new();

        public Task<string> CompleteAsync(AssistantCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Throw is null ? Task.FromResult(Answer!()) : Task.FromException<string>(Throw);
        }

        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private (VoiceSearchService Service, StubAssistant Assistant) CreateService(StubAssistant? assistant = null, VoiceSearchSettings? settings = null)
    {
        assistant ??= new StubAssistant();
        var airports = new FakeAirportRepository(_data.Baku, _data.London, _data.Istanbul);
        var service = new VoiceSearchService(airports, assistant, Options.Create(settings ?? new VoiceSearchSettings()), _clock, NullLogger<VoiceSearchService>.Instance);
        return (service, assistant);
    }

    private static VoiceSearchRequestDto Say(string text, string? language = null) => new() { Text = text, Language = language };

    [Fact]
    public async Task WhenTheRulesFindTheRoute_TheAssistantIsNotAsked()
    {
        var (service, assistant) = CreateService();
        assistant.Answer = () => throw new InvalidOperationException("must not be called");

        var result = await service.InterpretAsync(Say("Baku to London tomorrow"));

        Assert.Equal("rules", result.InterpretedBy);
        Assert.Empty(assistant.Requests);
        Assert.Equal("LHR", result.Destination!.Code);
    }

    [Fact]
    public async Task WhenTheRulesFindNoRoute_TheAssistantMayReadTheSentence_AndItsAnswerIsChecked()
    {
        var (service, assistant) = CreateService();
        assistant.Answer = () => "```json\n{\"origin\":\"Baku\",\"destination\":\"London\",\"date\":\"2026-11-20\",\"timeOfDay\":\"evening\",\"cabinClass\":\"business\",\"passengers\":2,\"cheapest\":false}\n```";

        var result = await service.InterpretAsync(Say("I need to get to the British capital from the Azerbaijani one around the 20th of next month"));

        Assert.Equal("assistant", result.InterpretedBy);
        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.Equal("GYD", result.Origin!.Code);
        Assert.Equal("LHR", result.Destination!.Code);
        Assert.Equal(new DateOnly(2026, 11, 20), result.Date);
        Assert.Equal(DepartureTimeOfDay.Evening, result.TimeOfDay);
        Assert.Equal(CabinClass.Business, result.CabinClass);
        Assert.Equal(2, result.Passengers);
        Assert.True(result.PassengersSpecified);

        var request = Assert.Single(assistant.Requests);
        Assert.Contains("Today is 2026-10-29", request.System);
        Assert.Contains("untrusted", request.System);
        Assert.Equal("user", Assert.Single(request.Turns).Role);
    }

    [Theory]
    [InlineData("{\"origin\":\"Atlantis\",\"destination\":\"London\"}")]
    [InlineData("{\"origin\":\"Baku\",\"destination\":\"Baku\"}")]
    [InlineData("{\"origin\":null,\"destination\":\"London\"}")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{\"origin\":")]
    public async Task AnAnswerOfTheAssistantThatIsNotUsable_LeavesTheRulesAnswer(string answer)
    {
        var (service, assistant) = CreateService();
        assistant.Answer = () => answer;

        var result = await service.InterpretAsync(Say("something the rules cannot read"));

        Assert.Equal("rules", result.InterpretedBy);
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        Assert.Contains(result.Issues, i => i.Code == "missing_origin");
    }

    [Fact]
    public async Task APastOrFarDate_FromTheAssistant_IsNotUsed()
    {
        var (service, assistant) = CreateService();
        assistant.Answer = () => "{\"origin\":\"Baku\",\"destination\":\"London\",\"date\":\"2025-01-01\",\"passengers\":40,\"cabinClass\":\"First\"}";

        var result = await service.InterpretAsync(Say("my usual trip"));

        Assert.Equal("assistant", result.InterpretedBy);
        Assert.Null(result.Date);
        Assert.True(result.DefaultPeriod);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
        Assert.Equal(1, result.Passengers);
        Assert.False(result.PassengersSpecified);
        Assert.Null(result.CabinClass);
    }

    [Fact]
    public async Task WhenTheAssistantIsDown_VoiceSearchStillWorks_ByTheRules()
    {
        var (service, assistant) = CreateService();
        assistant.Throw = new ServiceUnavailableException("The assistant is not available right now. Please try again later.", "assistant_key_rejected");

        var routeFound = await service.InterpretAsync(Say("Bakıdan Londona sabah"));
        var nothingFound = await service.InterpretAsync(Say("my usual trip"));

        Assert.Equal(VoiceSearchInterpretationDto.Ready, routeFound.Status);
        Assert.Equal("rules", routeFound.InterpretedBy);
        Assert.Equal("rules", nothingFound.InterpretedBy);
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, nothingFound.Status);
    }

    [Fact]
    public async Task WithoutAnAssistantKey_OrWhenSwitchedOff_NothingIsAsked()
    {
        var (service, assistant) = CreateService(new StubAssistant { IsConfigured = false });
        await service.InterpretAsync(Say("my usual trip"));
        Assert.Empty(assistant.Requests);

        var (off, offAssistant) = CreateService(settings: new VoiceSearchSettings { UseAssistantForComplexRequests = false });
        await off.InterpretAsync(Say("my usual trip"));
        Assert.Empty(offAssistant.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptySpeech_IsNeverSentToTheAssistant(string text)
    {
        var (service, assistant) = CreateService();

        var result = await service.InterpretAsync(Say(text));

        Assert.Equal("missing_text", Assert.Single(result.Issues).Code);
        Assert.Empty(assistant.Requests);
    }

    [Fact]
    public async Task ARejectedDate_IsNotOverruledByTheAssistant()
    {
        var (service, assistant) = CreateService();
        assistant.Answer = () => "{\"origin\":\"Baku\",\"destination\":\"London\"}";

        var result = await service.InterpretAsync(Say("the same trip on 2026-10-01"));

        Assert.Empty(assistant.Requests);
        Assert.Contains(result.Issues, i => i.Code == "past_date");
    }
}
