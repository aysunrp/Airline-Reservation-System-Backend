using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.VoiceSearch;

namespace Service.Tests;

/// <summary>
/// The Azerbaijani wishes of a flight request that go beyond the route and the day: the fastest flight, direct or connecting, a return day,
/// who travels (adults, children, infants), "N gün sonra" and a place that is not one of ours. Today is Thursday 29 October 2026.
/// </summary>
public class VoiceSearchAzerbaijaniInterpreterTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Airport[] Network =
    {
        new() { Id = 1, Code = "GYD", Name = "Heydar Aliyev", City = "Baku", CountryCode = "AZ", TimeZoneId = "Asia/Baku" },
        new() { Id = 2, Code = "LHR", Name = "Heathrow", City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" },
        new() { Id = 3, Code = "DXB", Name = "Dubai International", City = "Dubai", CountryCode = "AE", TimeZoneId = "Asia/Dubai" },
        new() { Id = 4, Code = "IST", Name = "Istanbul Airport", City = "Istanbul", CountryCode = "TR", TimeZoneId = "Europe/Istanbul" }
    };

    private static VoiceSearchInterpretationDto Read(string text)
        => new VoiceSearchInterpreter(new VoiceSearchSettings()).Interpret(text, "az", Network, Now);

    private static string[] Codes(VoiceSearchInterpretationDto result) => result.Issues.Select(i => i.Code).ToArray();

    // ---- the fastest flight ----

    [Theory]
    [InlineData("Bakıdan Londona ən sürətli uçuş")]
    [InlineData("bakidan londona en suretli ucus")]
    [InlineData("Bakıdan Londona ən qısa uçuş")]
    public void TheFastestFlight_IsRecognised_AndIsNotTheCheapest(string text)
    {
        var result = Read(text);

        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.True(result.Fastest);
        Assert.False(result.Cheapest);
        Assert.Equal("GYD", result.Origin?.Code);
        Assert.Equal("LHR", result.Destination?.Code);
    }

    [Fact]
    public void AFlightRequest_WithoutTheWord_IsNotTheFastest()
    {
        Assert.False(Read("Bakıdan Londona sabah").Fastest);
        Assert.False(Read("Bakıdan Londona ən ucuz uçuş").Fastest);
    }

    [Fact]
    public void TheFastestFlight_OfOneDay_KeepsTheDay()
    {
        var result = Read("Sabah Bakıdan Londona ən sürətli uçuş");

        Assert.True(result.Fastest);
        Assert.Equal(new DateOnly(2026, 10, 30), result.Date);
    }

    // ---- direct and connecting ----

    [Theory]
    [InlineData("Bakıdan Londona birbaşa reys")]
    [InlineData("Bakıdan Londona aktarmasız uçuş")]
    [InlineData("Bakıdan Londona transfersiz reys")]
    [InlineData("Bakıdan Londona dayanacaqsız")]
    public void DirectFlights_AreRecognised(string text)
        => Assert.Equal(VoiceSearchInterpretationDto.StopsDirect, Read(text).Stops);

    [Theory]
    [InlineData("Bakıdan Londona transferli reys göstər")]
    [InlineData("Bakıdan Londona aktarmalı uçuş")]
    [InlineData("Bakıdan Londona dayanacaqlı")]
    public void ConnectingFlights_AreRecognised(string text)
        => Assert.Equal(VoiceSearchInterpretationDto.StopsConnecting, Read(text).Stops);

    [Fact]
    public void DirectOrConnecting_AskedTogether_IsNoPreference()
    {
        var result = Read("Bakıdan Romaya birbaşa və ya transferli reys");

        Assert.Null(result.Stops);
    }

    [Fact]
    public void TheCheapestDirectFlight_KeepsBothWishes()
    {
        var result = Read("Bakıdan Londona ən ucuz birbaşa reys");

        Assert.True(result.Cheapest);
        Assert.True(result.CheapestSingle);
        Assert.Equal(VoiceSearchInterpretationDto.StopsDirect, result.Stops);
    }

    [Fact]
    public void ARequestWithoutAWordForStops_HasNoStopsPreference()
        => Assert.Null(Read("Bakıdan Londona sabah").Stops);

    // ---- the return day ----

    [Theory]
    [InlineData("Bakıdan Londona 10 noyabr gediş 15 noyabr qayıdış")]
    [InlineData("Bakıdan Londona 10 noyabr, qayıdış 15 noyabr")]
    [InlineData("Bakıdan Londona noyabrın 10-da gedib 15-də qayıdıram")]
    [InlineData("Bakıdan Londona 10.11.2026 gediş 15.11.2026 qayıdış")]
    public void ARoundTrip_GetsItsDepartureDay_AndItsReturnDay(string text)
    {
        var result = Read(text);

        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.Equal(new DateOnly(2026, 11, 10), result.Date);
        Assert.Equal(new DateOnly(2026, 11, 15), result.ReturnDate);
    }

    [Fact]
    public void AOneWayRequest_HasNoReturnDay()
    {
        var result = Read("Bakıdan Londona 10 noyabr");

        Assert.Equal(new DateOnly(2026, 11, 10), result.Date);
        Assert.Null(result.ReturnDate);
    }

    [Theory]
    [InlineData("Bakıdan Londona gedib-gəlmə bilet 10 noyabr")]
    [InlineData("Bakıdan Londona gediş-dönüş 10 noyabr")]
    public void ARoundTripWithoutAReturnDay_IsAskedAbout_NotGuessed(string text)
    {
        var result = Read(text);

        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        Assert.Contains("missing_return_date", Codes(result));
        Assert.Equal(new DateOnly(2026, 11, 10), result.Date);
        Assert.Null(result.ReturnDate);
    }

    [Fact]
    public void AReturnBeforeTheDeparture_IsRefused()
    {
        var result = Read("Bakıdan Londona 15 noyabr gediş 10 noyabr qayıdış");

        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        var issue = Assert.Single(result.Issues, i => i.Code == "return_before_departure");
        Assert.Equal("2026-11-10", issue.Value);
        Assert.Null(result.ReturnDate);
    }

    [Fact]
    public void AReturnDay_WithoutOneExactDepartureDay_IsAskedAbout()
    {
        var result = Read("Bakıdan Londona ən ucuz uçuş qayıdış 15 noyabr");

        Assert.Contains("return_needs_departure_date", Codes(result));
        Assert.Null(result.ReturnDate);
    }

    [Fact]
    public void AReturnDay_OfAnEarlierYear_IsRefused()
    {
        var result = Read("Bakıdan Londona 10 noyabr gediş 15 oktyabr 2026 qayıdış");

        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
        Assert.Contains("return_before_departure", Codes(result));
        Assert.Null(result.ReturnDate);
    }

    // ---- who travels ----

    [Fact]
    public void AdultsChildrenAndInfants_AreCountedApart()
    {
        var result = Read("Bakıdan Londona iki böyük bir uşaq bir körpə");

        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.Equal((2, 1, 1), (result.Adults, result.Children, result.Infants));
        Assert.Equal(4, result.Passengers);
        Assert.True(result.PassengersSpecified);
    }

    [Fact]
    public void TwoAdultsAndAChild_Is3Passengers()
    {
        var result = Read("Bakıdan Londona iki böyük bir uşaq");

        Assert.Equal((2, 1, 0), (result.Adults, result.Children, result.Infants));
        Assert.Equal(3, result.Passengers);
    }

    [Fact]
    public void AChildWithoutAnAdult_IsAskedAbout_NotGuessed()
    {
        var result = Read("Bakıdan Londona bir uşaq");

        Assert.Contains("missing_adult", Codes(result));
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
    }

    [Fact]
    public void MoreInfantsThanAdults_IsRefused()
    {
        var result = Read("Bakıdan Londona bir böyük iki körpə");

        var issue = Assert.Single(result.Issues, i => i.Code == "too_many_infants");
        Assert.Equal("2", issue.Value);
    }

    [Fact]
    public void MoreThanNineTravellers_IsRefused()
    {
        var result = Read("Bakıdan Londona beş böyük üç uşaq iki körpə");

        var issue = Assert.Single(result.Issues, i => i.Code == "bad_passengers");
        Assert.Equal("10", issue.Value);
    }

    [Fact]
    public void ANumberOfPeople_IsStillReadAsAdults()
    {
        var result = Read("Bakıdan Londona iki nəfər");

        Assert.Equal((2, 0, 0), (result.Adults, result.Children, result.Infants));
        Assert.Equal(2, result.Passengers);
    }

    [Fact]
    public void NoOneSaid_IsOneAdult()
    {
        var result = Read("Bakıdan Londona sabah");

        Assert.Equal((1, 0, 0), (result.Adults, result.Children, result.Infants));
        Assert.False(result.PassengersSpecified);
    }

    // ---- "N gün sonra" ----

    [Theory]
    [InlineData("Bakıdan Londona 2 gün sonra", 2026, 10, 31)]
    [InlineData("Bakıdan Londona üç gün sonra", 2026, 11, 1)]
    [InlineData("Bakıdan Londona bir gün sonra", 2026, 10, 30)]
    [InlineData("Bakıdan Londona iki həftə sonra", 2026, 11, 12)]
    public void NDaysAfterToday_IsOneDay_NotAPeriod(string text, int year, int month, int day)
    {
        var result = Read(text);

        Assert.Equal(new DateOnly(year, month, day), result.Date);
        Assert.Null(result.DateFrom);
        Assert.Null(result.DateTo);
        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
    }

    [Fact]
    public void TheNextNDays_IsStillAPeriod()
    {
        var result = Read("Bakıdan Londona gələn 5 gün");

        Assert.Null(result.Date);
        Assert.Equal(new DateOnly(2026, 10, 29), result.DateFrom);
        Assert.Equal(new DateOnly(2026, 11, 2), result.DateTo);
    }

    // ---- a place that is not one of ours ----

    [Theory]
    [InlineData("Bakıdan Atlantidaya uçuş", "Atlantidaya")]
    [InlineData("bakıdan atlantidaya sabah", "atlantidaya")]
    [InlineData("Atlantidaya uçuş", "Atlantidaya")]
    public void AnUnknownDestination_IsNamed(string text, string name)
    {
        var result = Read(text);

        var issue = Assert.Single(result.Issues, i => i.Code == "unknown_destination");
        Assert.Equal(name, issue.Value);
        Assert.DoesNotContain("missing_destination", Codes(result));
        Assert.Equal(VoiceSearchInterpretationDto.NeedsClarification, result.Status);
    }

    [Fact]
    public void AnUnknownOrigin_IsNamed()
    {
        var result = Read("Atlantidadan Londona uçuş");

        var issue = Assert.Single(result.Issues, i => i.Code == "unknown_origin");
        Assert.Equal("Atlantidadan", issue.Value);
        Assert.Equal("LHR", result.Destination?.Code);
        Assert.DoesNotContain("missing_origin", Codes(result));
    }

    [Theory]
    [InlineData("Bakıdan günorta")]
    [InlineData("Bakıdan sabah")]
    [InlineData("Bakıdan axşama")]
    [InlineData("Bakıdan ən ucuz uçuşa")]
    public void EverydayWords_AreNeverTakenForAPlace(string text)
    {
        var result = Read(text);

        Assert.DoesNotContain("unknown_destination", Codes(result));
        Assert.Contains("missing_destination", Codes(result));
    }

    [Fact]
    public void AKnownRoute_NeverGetsAnUnknownCity()
    {
        var result = Read("Sabah Bakıdan Londona iki nəfərlik uçuş");

        Assert.Equal(VoiceSearchInterpretationDto.Ready, result.Status);
        Assert.Empty(result.Issues);
    }
}

/// <summary>The period search behind "the fastest flight" and "direct flights": every flight of the period is compared, not the first 50.</summary>
public class VoiceSearchAzerbaijaniRangeTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
    private readonly TestData _data = new();
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly List<Flight> _flights = new();

    private FlightSearchService CreateSearch()
    {
        var settings = Options.Create(new VoiceSearchSettings());
        var repository = new FakeFlightRepository(new[] { _data.Outbound, _data.Return }.Concat(_flights).ToArray());
        return new(_data.Airports(), repository, _data.FlightSeats(), new FlightSearchRequestValidator(_clock), new LowestFaresRequestValidator(),
            new FlightRangeSearchRequestValidator(_clock, settings), settings, _clock);
    }

    private static FlightRangeSearchRequestDto Range(string sort, string? stops = null) => new()
    {
        OriginCode = "GYD", DestinationCode = "LHR", From = new DateOnly(2026, 10, 29), To = new DateOnly(2026, 12, 29), SortBy = sort, Stops = stops
    };

    /// <summary>A Baku to London flight of the given length with the given number of segments (a stop where one ends and the next begins).</summary>
    private Flight AddFlight(int id, DateTime departureUtc, int minutes, decimal economy, int segments = 1)
    {
        var template = _data.Outbound;
        var arrival = departureUtc.AddMinutes(minutes);
        var flight = new Flight
        {
            Id = id, FlightNumber = $"AV {id}", Route = template.Route, RouteId = template.RouteId, Aircraft = template.Aircraft, AircraftId = template.AircraftId,
            DepartureAt = departureUtc, ArrivalAt = arrival
        };
        for (var n = 0; n < segments; n++)
        {
            var from = departureUtc.AddMinutes(minutes * n / segments);
            var to = departureUtc.AddMinutes(minutes * (n + 1) / segments);
            flight.Segments.Add(new FlightSegment { SequenceNo = n + 1, Origin = n == 0 ? _data.Baku : _data.London, Destination = _data.London, DepartureAt = from, ArrivalAt = to });
        }

        flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = economy });
        var next = _data.Seats.Max(s => s.Id) + 1;
        foreach (var model in _data.Seats.Where(s => s.FlightId == _data.Outbound.Id && s.AircraftSeat.CabinClass == CabinClass.Economy).ToList())
        {
            _data.Seats.Add(new FlightSeat { Id = next++, FlightId = id, Flight = flight, AircraftSeatId = model.AircraftSeatId, AircraftSeat = model.AircraftSeat });
        }

        _flights.Add(flight);
        return flight;
    }

    [Fact]
    public async Task TheShortestFlight_ComesFirst_WhateverThePriceAndTheDay()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 420, 300);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 250, 900);
        AddFlight(13, new DateTime(2026, 11, 5, 5, 0, 0, DateTimeKind.Utc), 250, 500);

        var result = await CreateSearch().SearchRangeAsync(Range("duration"));

        // 250 minutes beats the test network's own flight and flight 11; of the two equal lengths the cheaper comes first.
        Assert.Equal(new[] { 13, 12 }, result.Flights.Take(2).Select(f => f.Id));
        Assert.True(result.Flights.Zip(result.Flights.Skip(1), (a, b) => a.DurationMinutes <= b.DurationMinutes).All(x => x));
        Assert.Equal("duration", result.SortBy);
    }

    [Fact]
    public async Task TheShortestFlight_IsChosenFromTheWholePeriod_NotFromTheFirst50()
    {
        // 58 flights, one a day; the shortest of all of them is the last, far beyond the 50 that a list can hold.
        for (var day = 0; day < 58; day++)
        {
            AddFlight(100 + day, new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc).AddDays(day), day == 57 ? 200 : 400, 300);
        }

        var result = await CreateSearch().SearchRangeAsync(Range("duration"));

        Assert.Equal(157, result.Flights[0].Id);
        Assert.Equal(200, result.Flights[0].DurationMinutes);
        Assert.Equal(50, result.Flights.Count);
        Assert.Equal(59, result.TotalFound);
    }

    [Fact]
    public async Task DirectFlights_AreThoseWithoutAStop()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 400, 300, segments: 2);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 400, 350);

        var direct = await CreateSearch().SearchRangeAsync(Range("price", "direct"));

        Assert.DoesNotContain(direct.Flights, f => f.Id == 11);
        Assert.All(direct.Flights, f => Assert.Equal(0, f.Stops));
        Assert.Contains(direct.Flights, f => f.Id == 12);
        Assert.Equal("direct", direct.Stops);
    }

    [Fact]
    public async Task ConnectingFlights_AreThoseWithAStop()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 400, 300, segments: 2);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 400, 350);

        var connecting = await CreateSearch().SearchRangeAsync(Range("price", "connecting"));

        // The test network's own flight (id 1) stops in Istanbul too, so it counts as connecting; flight 12 is direct and is left out.
        Assert.Equal(new[] { 11, 1 }, connecting.Flights.Select(f => f.Id));
        Assert.Equal(2, connecting.TotalFound);
        Assert.All(connecting.Flights, f => Assert.Equal(1, f.Stops));
    }

    [Fact]
    public async Task WithoutAStopsFilter_BothKindsAreReturned()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 400, 300, segments: 2);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 400, 350);

        var all = await CreateSearch().SearchRangeAsync(Range("price"));

        Assert.Contains(all.Flights, f => f.Id == 11);
        Assert.Contains(all.Flights, f => f.Stops == 0);
        Assert.Null(all.Stops);
    }

    [Fact]
    public async Task TheFastestDirectFlight_CombinesTheTwo()
    {
        AddFlight(11, new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), 100, 300, segments: 2);
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 300, 350);

        var result = await CreateSearch().SearchRangeAsync(Range("duration", "direct"));

        // Flight 11 is the shortest but has a stop, so it is not offered; the shortest direct one is.
        Assert.Equal(12, result.Flights[0].Id);
        Assert.DoesNotContain(result.Flights, f => f.Id == 11);
    }

    // ---- journeys of two flights in a period search ----

    private readonly Route _toIstanbul;
    private readonly Route _fromIstanbul;

    /// <summary>One leg of a journey through Istanbul: a flight on its own route with a fare and the Economy seats a booking needs.</summary>
    private Flight AddLeg(int id, Route route, Airport from, Airport to, DateTime departureUtc, int minutes, decimal economy)
    {
        var template = _data.Outbound;
        var flight = new Flight
        {
            Id = id, FlightNumber = $"AV {id}", Route = route, RouteId = route.Id, Aircraft = template.Aircraft, AircraftId = template.AircraftId,
            DepartureAt = departureUtc, ArrivalAt = departureUtc.AddMinutes(minutes)
        };
        flight.Segments.Add(new FlightSegment { SequenceNo = 1, Origin = from, Destination = to, DepartureAt = departureUtc, ArrivalAt = departureUtc.AddMinutes(minutes) });
        flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = economy });
        var next = _data.Seats.Max(s => s.Id) + 1;
        foreach (var model in _data.Seats.Where(s => s.FlightId == _data.Outbound.Id && s.AircraftSeat.CabinClass == CabinClass.Economy).ToList())
        {
            _data.Seats.Add(new FlightSeat { Id = next++, FlightId = id, Flight = flight, AircraftSeatId = model.AircraftSeatId, AircraftSeat = model.AircraftSeat });
        }

        _flights.Add(flight);
        return flight;
    }

    /// <summary>Baku to Istanbul at 07:00 (Baku), then Istanbul to London two hours after landing: 150 + 120 + 240 minutes, fares 100 and 150.</summary>
    private void AddJourney(int number, DateTime day)
    {
        var first = AddLeg(300 + number * 2, _toIstanbul, _data.Baku, _data.Istanbul, day.AddHours(3), 150, 100);
        AddLeg(301 + number * 2, _fromIstanbul, _data.Istanbul, _data.London, first.ArrivalAt.AddMinutes(120), 240, 150);
    }

    private FlightSearchService CreateSearchWithRoutes()
    {
        var settings = Options.Create(new VoiceSearchSettings());
        var repository = new FakeFlightRepository(new[] { _data.Outbound, _data.Return }.Concat(_flights).ToArray());
        var routes = new FakeRouteRepository(_data.Outbound.Route, _data.Return.Route, _toIstanbul, _fromIstanbul);
        return new(_data.Airports(), repository, _data.FlightSeats(), new FlightSearchRequestValidator(_clock), new LowestFaresRequestValidator(),
            new FlightRangeSearchRequestValidator(_clock, settings), settings, _clock, routes);
    }

    public VoiceSearchAzerbaijaniRangeTests()
    {
        _toIstanbul = new Route { Id = 11, OriginAirportId = _data.Baku.Id, Origin = _data.Baku, DestinationAirportId = _data.Istanbul.Id, Destination = _data.Istanbul, IsActive = true };
        _fromIstanbul = new Route { Id = 12, OriginAirportId = _data.Istanbul.Id, Origin = _data.Istanbul, DestinationAirportId = _data.London.Id, Destination = _data.London, IsActive = true };
    }

    [Fact]
    public async Task EveryJourneyOfThePeriod_IsListed_NotJustTheOneDaysCap()
    {
        // 40 days with one journey each: more than the 30 a one-day search keeps, fewer than the 50 a list holds.
        for (var n = 0; n < 40; n++)
        {
            AddJourney(n, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc).AddDays(n));
        }

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("price"));

        Assert.Equal(40, result.Connections.Count);
        Assert.Equal(41, result.TotalFound); // the 40 journeys and the test network's own flight with a stop
        Assert.All(result.Connections, c => Assert.Equal(("IST", 2), (c.TransferCode, c.Flights.Count)));
        Assert.All(result.Connections, c => Assert.Equal(250m, c.Fares[CabinClass.Economy]));
        Assert.Equal(40, result.Connections.Select(c => c.DepartureAt.Date).Distinct().Count());
    }

    [Fact]
    public async Task AJourney_TakesPartInThePriceOrder_WithTheFlights()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("price"));

        // 250 for the journey against 365 for the test network's flight: the journey is the cheapest of the period.
        Assert.Equal(250m, result.Connections[0].Fares[CabinClass.Economy]);
        Assert.Equal(2, result.TotalFound);
        Assert.Equal(1, result.LowestPriceCount);
    }

    [Fact]
    public async Task TheCheapestOnly_CanBeAJourney()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        var request = Range("price");
        request.LowestOnly = true;

        var result = await CreateSearchWithRoutes().SearchRangeAsync(request);

        Assert.Empty(result.Flights);
        Assert.Single(result.Connections);
    }

    [Fact]
    public async Task TheShortest_ComparesJourneysWithFlights()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("duration"));

        // 510 minutes for the journey against 535 for the test network's flight.
        Assert.Equal(510, result.Connections[0].DurationMinutes);
        Assert.Equal(535, result.Flights[0].DurationMinutes);
    }

    [Fact]
    public async Task DirectOnly_LeavesTheJourneysOut()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("price", "direct"));

        Assert.Empty(result.Connections);
        Assert.Equal(0, result.TotalFound); // the test network's flight stops in Istanbul, so it is not direct either
    }

    [Fact]
    public async Task Connecting_KeepsTheJourneys_AndTheFlightsWithAStop()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        AddFlight(12, new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), 400, 350);

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("price", "connecting"));

        Assert.Single(result.Connections);
        Assert.Equal(new[] { 1 }, result.Flights.Select(f => f.Id)); // direct flight 12 is left out
        Assert.Equal(2, result.TotalFound);
    }

    [Fact]
    public async Task APartOfTheDay_AppliesToTheFirstFlightOfAJourney()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)); // 03:00 UTC is 07:00 in Baku: morning
        var search = CreateSearchWithRoutes();

        var morning = Range("price");
        morning.TimeOfDay = DepartureTimeOfDay.Morning;
        var evening = Range("price");
        evening.TimeOfDay = DepartureTimeOfDay.Evening;

        Assert.Single((await search.SearchRangeAsync(morning)).Connections);
        Assert.Empty((await search.SearchRangeAsync(evening)).Connections);
    }

    [Fact]
    public async Task WithoutARouteRepository_ThePeriodHasNoJourneys_AsBefore()
    {
        AddJourney(0, new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = await CreateSearch().SearchRangeAsync(Range("price"));

        Assert.Empty(result.Connections);
    }

    [Fact]
    public async Task AJourneyWithTooShortATransfer_IsNotOffered()
    {
        var first = AddLeg(400, _toIstanbul, _data.Baku, _data.Istanbul, new DateTime(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc), 150, 100);
        AddLeg(401, _fromIstanbul, _data.Istanbul, _data.London, first.ArrivalAt.AddMinutes(60), 240, 150); // an hour: under the 90 minutes a transfer needs

        var result = await CreateSearchWithRoutes().SearchRangeAsync(Range("price"));

        Assert.Empty(result.Connections);
    }

    [Fact]
    public async Task AnUnknownSortOrStops_IsRefused()
    {
        var search = CreateSearch();

        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("random")));
        await Assert.ThrowsAsync<ValidationException>(() => search.SearchRangeAsync(Range("price", "sometimes")));
        await search.SearchRangeAsync(Range("duration"));
    }
}
