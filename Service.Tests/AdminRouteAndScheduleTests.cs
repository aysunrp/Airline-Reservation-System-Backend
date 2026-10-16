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
        new Aircraft { Id = 1, Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08" },
        new Aircraft { Id = 2, Name = "AEROVA Pearl", Model = "A321neo", RegistrationNumber = "4K-AV21", Status = AircraftStatus.Inactive });
    private readonly FakeFlightScheduleRepository _schedules = new();

    private AdminRouteService CreateRouteService() => new(_routes, _data.Airports(), new RouteRequestValidator());

    private AdminScheduleService CreateScheduleService() => new(_schedules, _routes, _aircraft, new ScheduleRequestValidator());

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
        ValidTo = new DateOnly(2026, 12, 31)
    };

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
}
