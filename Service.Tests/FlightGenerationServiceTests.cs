using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

public class FlightGenerationServiceTests
{
    // Sunday 1 Nov 2026, 12:00 UTC.
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestData _data = new();
    private readonly Route _route;
    private readonly Aircraft _aircraft;
    private readonly FakeFlightRepository _flights = new();
    private readonly FakeFlightScheduleRepository _schedules = new();
    private readonly FakeAircraftRepository _aircraftRepository;

    public FlightGenerationServiceTests()
    {
        _route = new Route { Id = 10, Origin = _data.Baku, OriginAirportId = _data.Baku.Id, Destination = _data.London, DestinationAirportId = _data.London.Id };
        _aircraft = new Aircraft { Id = 1, Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08" };
        foreach (var (id, row, letter, cabin) in new[]
                 {
                     (1, 1, "A", CabinClass.Business), (2, 1, "B", CabinClass.Business),
                     (3, 11, "A", CabinClass.Economy), (4, 11, "B", CabinClass.Economy), (5, 11, "C", CabinClass.Economy)
                 })
        {
            _aircraft.Seats.Add(new AircraftSeat { Id = id, AircraftId = 1, RowNumber = row, Letter = letter, CabinClass = cabin });
        }

        _aircraftRepository = new FakeAircraftRepository(_aircraft,
            new Aircraft { Id = 2, Name = "AEROVA Pearl", Model = "A321neo", RegistrationNumber = "4K-AV21", Status = AircraftStatus.Maintenance });
    }

    private FlightGenerationService CreateService(int daysAhead = 90)
        => new(_schedules, _flights, _aircraftRepository, Options.Create(new FlightGenerationSettings { DaysAhead = daysAhead }), new FixedTimeProvider(Now));

    private FlightSchedule AddSchedule(Action<FlightSchedule>? change = null)
    {
        var schedule = new FlightSchedule
        {
            FlightNumber = "AV 101",
            Route = _route,
            RouteId = _route.Id,
            DefaultAircraftId = _aircraft.Id,
            DepartureTime = new TimeOnly(9, 30),
            ArrivalTime = new TimeOnly(13, 10),
            DaysOfWeek = ScheduleDays.Monday | ScheduleDays.Friday,
            ValidFrom = new DateOnly(2026, 11, 1),
            ValidTo = new DateOnly(2026, 11, 30)
        };
        schedule.Fares.Add(new FlightScheduleFare { CabinClass = CabinClass.Economy, BasePrice = 420 });
        schedule.Fares.Add(new FlightScheduleFare { CabinClass = CabinClass.Business, BasePrice = 890 });
        change?.Invoke(schedule);
        _schedules.AddAsync(schedule).GetAwaiter().GetResult();
        return schedule;
    }

    private static DateTime Utc(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OperatingDays_GetFlightsWithUtcTimesFaresAndSeats()
    {
        var schedule = AddSchedule();

        var result = await CreateService().GenerateAsync(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 14));

        // Mondays 2 and 9, Fridays 6 and 13.
        Assert.Equal(new[] { 2, 6, 9, 13 }, result.Created.Select(c => c.ScheduledDate.Day));
        Assert.Empty(result.Skipped);

        var flight = (await _flights.GetAllWithDetailsAsync()).First();
        Assert.Equal(("AV 101", schedule.Id, _route.Id, _aircraft.Id, FlightStatus.Scheduled),
            (flight.FlightNumber, flight.FlightScheduleId, flight.RouteId, flight.AircraftId, flight.Status));
        Assert.Equal((Utc(11, 2, 5, 30), Utc(11, 2, 13, 10)), (flight.DepartureAt, flight.ArrivalAt));

        var segment = Assert.Single(flight.Segments);
        Assert.Equal((1, _data.Baku.Id, _data.London.Id, flight.DepartureAt, flight.ArrivalAt),
            (segment.SequenceNo, segment.OriginAirportId, segment.DestinationAirportId, segment.DepartureAt, segment.ArrivalAt));

        Assert.Equal(new Dictionary<CabinClass, decimal> { [CabinClass.Economy] = 420, [CabinClass.Business] = 890 },
            flight.Fares.ToDictionary(f => f.CabinClass, f => f.BasePrice));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, flight.Seats.Select(s => s.AircraftSeatId).Order());
        Assert.All(flight.Seats, s => Assert.Equal(SeatStatus.Available, s.Status));
    }

    [Fact]
    public async Task Stopovers_BecomeOrderedSegments()
    {
        AddSchedule(s =>
        {
            s.DepartureTime = new TimeOnly(14, 20);
            s.ArrivalTime = new TimeOnly(19, 15);
            s.Stops.Add(new FlightScheduleStop { SequenceNo = 1, Airport = _data.Istanbul, AirportId = _data.Istanbul.Id, ArrivalOffsetMinutes = 190, LayoverMinutes = 80 });
        });

        await CreateService().GenerateAsync(from: new DateOnly(2026, 11, 2), to: new DateOnly(2026, 11, 2));

        var flight = Assert.Single(await _flights.GetAllWithDetailsAsync());
        Assert.Collection(flight.Segments.OrderBy(s => s.SequenceNo),
            s =>
            {
                Assert.Equal((_data.Baku.Id, _data.Istanbul.Id, 80), (s.OriginAirportId, s.DestinationAirportId, s.LayoverMinutes));
                Assert.Equal((Utc(11, 2, 10, 20), Utc(11, 2, 13, 30)), (s.DepartureAt, s.ArrivalAt));
            },
            s =>
            {
                Assert.Equal((_data.Istanbul.Id, _data.London.Id), (s.OriginAirportId, s.DestinationAirportId));
                Assert.Equal((Utc(11, 2, 14, 50), Utc(11, 2, 19, 15)), (s.DepartureAt, s.ArrivalAt));
            });
    }

    [Fact]
    public async Task RunningAgain_CreatesNothingNew()
    {
        AddSchedule();
        var service = CreateService();
        await service.GenerateAsync(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 14));

        var again = await service.GenerateAsync(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 14));

        Assert.Empty(again.Created);
        Assert.Equal(4, again.AlreadyExisting);
        Assert.Equal(4, (await _flights.GetAllWithDetailsAsync()).Count);
    }

    [Fact]
    public async Task ExistingFlightWithTheSameNumberAndDeparture_IsLeftAlone()
    {
        AddSchedule();
        await _flights.AddAsync(new Flight { FlightNumber = "AV 101", DepartureAt = Utc(11, 2, 5, 30), ArrivalAt = Utc(11, 2, 13, 10) });

        var result = await CreateService().GenerateAsync(from: new DateOnly(2026, 11, 2), to: new DateOnly(2026, 11, 6));

        Assert.Equal(1, result.AlreadyExisting);
        Assert.Equal(new[] { 6 }, result.Created.Select(c => c.ScheduledDate.Day));
        Assert.Null((await _flights.GetAllWithDetailsAsync()).First().FlightScheduleId);
    }

    [Fact]
    public async Task WithoutDates_TodayAndTheConfiguredDaysAheadAreCovered()
    {
        AddSchedule();

        // Today is Sunday 1 Nov in Baku; seven days run to Saturday 7 Nov.
        var result = await CreateService(daysAhead: 7).GenerateAsync();

        Assert.Equal(new[] { 2, 6 }, result.Created.Select(c => c.ScheduledDate.Day));
    }

    [Fact]
    public async Task DaysAlreadyDeparted_AreSkipped()
    {
        AddSchedule(s => s.ValidFrom = new DateOnly(2026, 10, 1));

        var result = await CreateService().GenerateAsync(from: new DateOnly(2026, 10, 30), to: new DateOnly(2026, 11, 2));

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(new DateOnly(2026, 10, 30), skipped.ScheduledDate);
        Assert.Equal(new[] { 2 }, result.Created.Select(c => c.ScheduledDate.Day));
    }

    [Fact]
    public async Task SchedulesThatCannotFly_AreSkippedWithAReason()
    {
        AddSchedule(s => { s.FlightNumber = "AV 201"; s.DefaultAircraftId = null; });
        AddSchedule(s => { s.FlightNumber = "AV 202"; s.DefaultAircraftId = 2; });
        AddSchedule(s => { s.FlightNumber = "AV 203"; s.Fares.Add(new FlightScheduleFare { CabinClass = CabinClass.Comfort, BasePrice = 560 }); });
        AddSchedule(s => { s.FlightNumber = "AV 204"; s.Fares.Clear(); });
        AddSchedule(s => { s.FlightNumber = "AV 205"; s.Route = new Route { Id = 11, Origin = _data.Baku, Destination = _data.Istanbul, IsActive = false }; s.RouteId = 11; });
        AddSchedule(s => { s.FlightNumber = "AV 206"; s.IsActive = false; });

        var result = await CreateService().GenerateAsync(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 14));

        Assert.Empty(result.Created);
        Assert.Equal(new[] { "AV 201", "AV 202", "AV 203", "AV 204", "AV 205" }, result.Skipped.Select(s => s.FlightNumber));
        Assert.All(result.Skipped, s => Assert.Null(s.ScheduledDate));
        Assert.Contains("Maintenance", result.Skipped[1].Reason);
        Assert.Contains("Comfort", result.Skipped[2].Reason);

        // An inactive schedule asked for by id says why nothing happened.
        var inactive = await CreateService().GenerateAsync(scheduleId: 6);
        Assert.Equal("The schedule is inactive.", Assert.Single(inactive.Skipped).Reason);
    }

    [Fact]
    public async Task ClockChanges_AreReported()
    {
        // London to Baku at 01:30 on 28 Mar 2027, the night UK clocks jump from 01:00 to 02:00.
        AddSchedule(s =>
        {
            s.Route = new Route { Id = 12, Origin = _data.London, OriginAirportId = _data.London.Id, Destination = _data.Baku, DestinationAirportId = _data.Baku.Id };
            s.RouteId = 12;
            s.DepartureTime = new TimeOnly(1, 30);
            s.ArrivalTime = new TimeOnly(9, 30);
            s.DaysOfWeek = ScheduleDays.Sunday;
            s.ValidTo = null;
        });

        var result = await CreateService().GenerateAsync(from: new DateOnly(2027, 3, 28), to: new DateOnly(2027, 3, 28));

        Assert.Equal(new DateTime(2027, 3, 28, 1, 30, 0, DateTimeKind.Utc), Assert.Single(result.Created).DepartureAt);
        Assert.Contains("does not exist", Assert.Single(result.TimeAdjustments));
    }

    [Fact]
    public async Task BadRequests_AreRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.GenerateAsync(from: new DateOnly(2026, 11, 10), to: new DateOnly(2026, 11, 1)));
        await Assert.ThrowsAsync<ValidationException>(() => service.GenerateAsync(from: new DateOnly(2026, 1, 1), to: new DateOnly(2027, 6, 1)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateAsync(scheduleId: 99));
    }
}
