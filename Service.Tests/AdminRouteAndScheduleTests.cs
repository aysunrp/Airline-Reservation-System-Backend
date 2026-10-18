using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class AdminRouteAndScheduleTests
{
    private readonly TestData _data = new();
    private readonly FakeRouteRepository _routes = new();
    private readonly FakeAircraftRepository _aircraft = new(
        new Aircraft { Id = 1, Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08", Seats = Seats(CabinClass.Business, CabinClass.Comfort, CabinClass.Economy) },
        new Aircraft { Id = 2, Name = "AEROVA Pearl", Model = "A321neo", RegistrationNumber = "4K-AV21", Status = AircraftStatus.Inactive },
        new Aircraft { Id = 3, Name = "AEROVA Silk", Model = "A320neo", RegistrationNumber = "4K-AV30", Seats = Seats(CabinClass.Economy) });
    private readonly FakeFlightScheduleRepository _schedules = new();
    private readonly Airport _vienna = new() { Id = 4, Code = "VIE", Name = "Vienna", City = "Vienna", CountryCode = "AT", TimeZoneId = "Europe/Vienna" };

    private AdminRouteService CreateRouteService() => new(_routes, _data.Airports(), new RouteRequestValidator());

    private AdminScheduleService CreateScheduleService()
        => new(_schedules, _routes, _aircraft, new FakeAirportRepository(_data.Baku, _data.London, _data.Istanbul, _vienna), new ScheduleRequestValidator());

    private static RouteRequestDto BakuLondon() => new() { OriginCode = "gyd", DestinationCode = "LHR", DistanceKm = 3950, DurationMinutes = 340 };

    private Task<RouteDto> CreateRouteAsync() => CreateRouteService().CreateAsync(BakuLondon());

    [Fact]
    public async Task CreateRoute_ResolvesAirportsAndBuildsCode()
    {
        var route = await CreateRouteAsync();

        Assert.Equal("GYD-LHR", route.Code);
        Assert.Equal("Baku", route.OriginCity);
        Assert.Equal("London", route.DestinationCity);
        Assert.Single(await CreateRouteService().GetAllAsync("london"));
        Assert.Empty(await CreateRouteService().GetAllAsync("paris"));
    }

    [Fact]
    public async Task DuplicateRoute_ThrowsConflict_AndBadInputIsRejected()
    {
        await CreateRouteAsync();

        await Assert.ThrowsAsync<ConflictException>(() => CreateRouteService().CreateAsync(BakuLondon()));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateRouteService().CreateAsync(new RouteRequestDto
        {
            OriginCode = "GYD", DestinationCode = "JFK", DistanceKm = 9000, DurationMinutes = 700
        }));
        await Assert.ThrowsAsync<ValidationException>(() => CreateRouteService().CreateAsync(new RouteRequestDto
        {
            OriginCode = "GYD", DestinationCode = "gyd", DistanceKm = 1, DurationMinutes = 1
        }));
    }

    [Fact]
    public async Task RouteInUse_KeepsAirportsAndCannotBeDeleted()
    {
        var route = await CreateRouteAsync();
        _routes.InUse.Add(route.Id);
        var service = CreateRouteService();

        var changedAirports = new RouteRequestDto { OriginCode = "GYD", DestinationCode = "IST", DistanceKm = 1760, DurationMinutes = 190 };
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(route.Id, changedAirports));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(route.Id));

        var deactivated = BakuLondon();
        deactivated.IsActive = false;
        Assert.False((await service.UpdateAsync(route.Id, deactivated)).IsActive);
    }

    private static ScheduleRequestDto Schedule(int routeId) => new()
    {
        FlightNumber = "av101",
        RouteId = routeId,
        DefaultAircraftId = 1,
        DepartureTime = new TimeOnly(16, 45),
        ArrivalTime = new TimeOnly(0, 20),
        ArrivalDayOffset = 1,
        DaysOfWeek = ScheduleDays.Monday | ScheduleDays.Friday,
        ValidFrom = new DateOnly(2026, 11, 1),
        ValidTo = new DateOnly(2026, 12, 31),
        Fares = { Fare(CabinClass.Economy, 420) }
    };

    private static ScheduleFareRequestDto Fare(CabinClass cabin, decimal price) => new() { CabinClass = cabin, BasePrice = price };

    private static ScheduleStopRequestDto Stop(int sequenceNo, string airportCode, int arrivalOffsetMinutes, int layoverMinutes = 60)
        => new() { SequenceNo = sequenceNo, AirportCode = airportCode, ArrivalOffsetMinutes = arrivalOffsetMinutes, LayoverMinutes = layoverMinutes };

    [Fact]
    public async Task CreateSchedule_NormalizesFlightNumber()
    {
        var route = await CreateRouteAsync();

        var schedule = await CreateScheduleService().CreateAsync(Schedule(route.Id));

        Assert.Equal("AV 101", schedule.FlightNumber);
        Assert.Equal("GYD-LHR", schedule.RouteCode);
        Assert.Equal("A350-900", schedule.AircraftModel);
        Assert.Equal(1, schedule.ArrivalDayOffset);
    }

    [Fact]
    public async Task ScheduleRules_AreEnforced()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();

        var sameDay = Schedule(route.Id);
        sameDay.ArrivalDayOffset = 0; // 16:45 -> 00:20 on the same day is impossible
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(sameDay));

        var noDays = Schedule(route.Id);
        noDays.DaysOfWeek = ScheduleDays.None;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(noDays));

        var inactiveAircraft = Schedule(route.Id);
        inactiveAircraft.DefaultAircraftId = 2;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(inactiveAircraft));

        var noStart = Schedule(route.Id);
        noStart.ValidFrom = default;
        noStart.ValidTo = null;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(noStart));

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Schedule(99)));
    }

    [Fact]
    public async Task DateFilter_ReturnsSchedulesOperatingThatDay()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();
        await service.CreateAsync(Schedule(route.Id));

        Assert.Single(await service.GetAllAsync(new DateOnly(2026, 11, 2)));  // Monday
        Assert.Single(await service.GetAllAsync(new DateOnly(2026, 11, 6)));  // Friday
        Assert.Empty(await service.GetAllAsync(new DateOnly(2026, 11, 3)));   // Tuesday
        Assert.Empty(await service.GetAllAsync(new DateOnly(2027, 1, 4)));    // Monday after ValidTo
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task CreateSchedule_StoresFaresForAllCabinsAndOrderedStops()
    {
        var route = await CreateRouteAsync();
        var request = Schedule(route.Id);
        request.Fares = new() { Fare(CabinClass.Business, 890), Fare(CabinClass.Economy, 420), Fare(CabinClass.Comfort, 560) };
        request.Stops = new() { Stop(2, "vie", 300, 45), Stop(1, "IST", 150, 80) };

        var schedule = await CreateScheduleService().CreateAsync(request);

        Assert.Equal(new Dictionary<CabinClass, decimal>
        {
            [CabinClass.Economy] = 420, [CabinClass.Comfort] = 560, [CabinClass.Business] = 890
        }, schedule.Fares);
        Assert.Equal(new[] { "IST", "VIE" }, schedule.Stops.Select(s => s.AirportCode));
        Assert.Equal(new[] { 1, 2 }, schedule.Stops.Select(s => s.SequenceNo));
        Assert.Equal(new[] { 150, 300 }, schedule.Stops.Select(s => s.ArrivalOffsetMinutes));
        Assert.Equal(new[] { 80, 45 }, schedule.Stops.Select(s => s.LayoverMinutes));
        Assert.Equal("Istanbul", schedule.Stops[0].City);
    }

    [Fact]
    public async Task FareRules_AreEnforced()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();

        foreach (var fares in new[]
                 {
                     new List<ScheduleFareRequestDto>(),
                     new List<ScheduleFareRequestDto> { Fare(CabinClass.Economy, 0) },
                     new List<ScheduleFareRequestDto> { Fare(CabinClass.Economy, -10) },
                     new List<ScheduleFareRequestDto> { Fare(CabinClass.Economy, 420), Fare(CabinClass.Economy, 380) },
                     new List<ScheduleFareRequestDto> { Fare((CabinClass)9, 420) }
                 })
        {
            var request = Schedule(route.Id);
            request.Fares = fares;
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
        }

        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task StopRules_AreEnforced()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();

        foreach (var stops in new[]
                 {
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", 150), Stop(1, "VIE", 300) },  // duplicate number
                     new List<ScheduleStopRequestDto> { Stop(0, "IST", 150) },                       // not positive
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", 150), Stop(3, "VIE", 300) },  // gap in numbering
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", -5) },                        // negative offset
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", 150, -1) },                   // negative layover
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", 150, 80), Stop(2, "VIE", 200) }, // lands before leaving IST
                     new List<ScheduleStopRequestDto> { Stop(1, "IST", 150), Stop(2, "ist", 300) },  // same airport twice
                     new List<ScheduleStopRequestDto> { Stop(1, "IS", 150) }                         // not an airport code
                 })
        {
            var request = Schedule(route.Id);
            request.Stops = stops;
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
        }

        foreach (var endpoint in new[] { "GYD", "LHR" })
        {
            var request = Schedule(route.Id);
            request.Stops = new() { Stop(1, endpoint, 150) };
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(request));
        }

        var unknownAirport = Schedule(route.Id);
        unknownAirport.Stops = new() { Stop(1, "JFK", 150) };
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(unknownAirport));

        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateSchedule_ChangesFaresAndStopsInPlace()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();
        var request = Schedule(route.Id);
        request.Fares = new() { Fare(CabinClass.Economy, 365), Fare(CabinClass.Comfort, 490), Fare(CabinClass.Business, 820) };
        request.Stops = new() { Stop(1, "IST", 150, 80), Stop(2, "VIE", 300, 45) };
        var created = await service.CreateAsync(request);
        var stored = (await _schedules.GetWithDetailsAsync(created.Id))!;
        var economy = stored.Fares.Single(f => f.CabinClass == CabinClass.Economy);
        var firstStop = stored.Stops.Single(s => s.SequenceNo == 1);

        request.Fares = new() { Fare(CabinClass.Economy, 399), Fare(CabinClass.Business, 820) };
        request.Stops = new() { Stop(1, "VIE", 200, 30) };
        var updated = await service.UpdateAsync(created.Id, request);

        Assert.Equal(new Dictionary<CabinClass, decimal> { [CabinClass.Economy] = 399, [CabinClass.Business] = 820 }, updated.Fares);
        var stop = Assert.Single(updated.Stops);
        Assert.Equal(("VIE", 200, 30), (stop.AirportCode, stop.ArrivalOffsetMinutes, stop.LayoverMinutes));
        Assert.Same(economy, stored.Fares.Single(f => f.CabinClass == CabinClass.Economy));
        Assert.Same(firstStop, stored.Stops.Single());

        request.Stops = new();
        Assert.Empty((await service.UpdateAsync(created.Id, request)).Stops);
    }

    private static List<AircraftSeat> Seats(params CabinClass[] cabins)
        => cabins.Select((cabin, row) => new AircraftSeat { RowNumber = row + 1, Letter = "A", CabinClass = cabin }).ToList();

    [Fact]
    public async Task Fares_MustMatchTheCabinsOfTheDefaultAircraft()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();

        var economyOnly = Schedule(route.Id);
        economyOnly.DefaultAircraftId = 3;
        economyOnly.Fares = new() { Fare(CabinClass.Economy, 210), Fare(CabinClass.Business, 480) };
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(economyOnly));
        Assert.Contains("Business", error.Message);

        economyOnly.Fares = new() { Fare(CabinClass.Economy, 210) };
        var schedule = await service.CreateAsync(economyOnly);

        // Moving the schedule to an aircraft without the priced cabins is rejected the same way.
        var withBusiness = Schedule(route.Id);
        withBusiness.Fares = new() { Fare(CabinClass.Economy, 210), Fare(CabinClass.Business, 480) };
        var moved = await service.UpdateAsync(schedule.Id, withBusiness);
        withBusiness.DefaultAircraftId = 3;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(schedule.Id, withBusiness));

        Assert.Equal("A350-900", moved.AircraftModel);
        Assert.Equal(2, moved.Fares.Count);
    }

    [Fact]
    public async Task EmptyFareOrStopEntries_AreValidationErrors()
    {
        var route = await CreateRouteAsync();
        var service = CreateScheduleService();

        var nullFare = Schedule(route.Id);
        nullFare.Fares.Add(null!);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(nullFare));

        var nullStop = Schedule(route.Id);
        nullStop.Stops = new() { null! };
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(nullStop));
    }
}
