using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class AdminFlightServiceTests
{
    private readonly BookingTestContext _context = new();
    private readonly FakeRouteRepository _routes;
    private readonly FakeAircraftRepository _aircraft;
    private readonly FakeFlightRepository _flights;

    public AdminFlightServiceTests()
    {
        var data = _context.Data;
        _routes = new FakeRouteRepository(data.Outbound.Route, data.Return.Route);

        // A321neo with 2 Business and 3 Economy seats; a second aircraft in maintenance.
        var a321 = new Aircraft { Id = 20, Name = "AEROVA Caspian", Model = "A321neo", RegistrationNumber = "4K-AV14" };
        foreach (var (id, row, letter, cabin) in new[]
                 {
                     (2001, 1, "A", CabinClass.Business), (2002, 1, "B", CabinClass.Business),
                     (2003, 3, "A", CabinClass.Economy), (2004, 3, "B", CabinClass.Economy), (2005, 3, "C", CabinClass.Economy)
                 })
        {
            a321.Seats.Add(new AircraftSeat { Id = id, RowNumber = row, Letter = letter, CabinClass = cabin, AircraftId = 20 });
        }

        var outboundAircraft = data.Outbound.Aircraft;
        foreach (var seat in data.Seats.Where(s => s.FlightId == data.Outbound.Id))
        {
            outboundAircraft.Seats.Add(seat.AircraftSeat);
        }

        _aircraft = new FakeAircraftRepository(
            outboundAircraft,
            a321,
            new Aircraft { Id = 21, Name = "AEROVA Pearl", Model = "A321neo", RegistrationNumber = "4K-AV21", Status = AircraftStatus.Maintenance });

        _flights = new FakeFlightRepository(data.Outbound, data.Return)
        {
            RouteLookup = id => _routes.GetWithAirportsAsync(id).Result,
            AircraftLookup = id => _aircraft.GetWithSeatsAsync(id).Result
        };
    }

    private AdminFlightService CreateService() => new(
        _flights,
        _context.FlightSeats(),
        _routes,
        _context.Data.Airports(),
        _aircraft,
        new FakeFlightScheduleRepository(),
        _context.Bookings,
        new NotificationService(_context.Notifications),
        new FlightRequestValidator(),
        _context.Clock);

    private static FlightRequestDto NewFlight(string number = "av318") => new()
    {
        FlightNumber = number,
        OriginCode = "GYD",
        DestinationCode = "LHR",
        AircraftId = 20,
        DepartureAt = new DateTime(2026, 11, 20, 8, 15, 0, DateTimeKind.Utc),
        ArrivalAt = new DateTime(2026, 11, 20, 13, 55, 0, DateTimeKind.Utc),
        Fares = { [CabinClass.Economy] = 410, [CabinClass.Business] = 950 }
    };

    private static FlightRequestDto Edit(Flight flight) => new()
    {
        FlightNumber = flight.FlightNumber,
        OriginCode = flight.Route.Origin.Code,
        DestinationCode = flight.Route.Destination.Code,
        AircraftId = flight.AircraftId,
        DepartureAt = flight.DepartureAt,
        ArrivalAt = flight.ArrivalAt,
        Status = flight.Status,
        Fares = flight.Fares.ToDictionary(f => f.CabinClass, f => f.BasePrice)
    };

    [Fact]
    public async Task Create_BuildsFaresSegmentAndSeatInventory()
    {
        var result = await CreateService().CreateAsync(NewFlight());

        Assert.Equal("AV 318", result.FlightNumber);
        Assert.Equal("Baku", result.OriginCity);
        Assert.Equal("A321neo", result.AircraftModel);
        Assert.Equal(0, result.Stops);
        Assert.Equal(340, result.DurationMinutes);
        Assert.Equal(410m, result.Fares[CabinClass.Economy]);

        var flight = (await _flights.GetByIdAsync(result.Id))!;
        Assert.Equal(5, flight.Seats.Count);
        Assert.All(flight.Seats, s => Assert.Equal(SeatStatus.Available, s.Status));
        Assert.Equal(new[] { 2001, 2002, 2003, 2004, 2005 }, flight.Seats.Select(s => s.AircraftSeatId));
    }

    [Fact]
    public async Task Create_RejectsInvalidSetups()
    {
        var service = CreateService();

        var noRoute = NewFlight();
        noRoute.DestinationCode = "IST";
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(noRoute));

        var maintenance = NewFlight();
        maintenance.AircraftId = 21;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(maintenance));

        var missingCabin = NewFlight();
        missingCabin.Fares[CabinClass.Comfort] = 600;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(missingCabin));

        var past = NewFlight();
        past.DepartureAt = TestData.Now.AddDays(-1);
        past.ArrivalAt = TestData.Now.AddDays(-1).AddHours(5);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(past));

        await service.CreateAsync(NewFlight());
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(NewFlight("AV 318")));
    }

    [Fact]
    public async Task FlightWithBookings_KeepsAircraftAndCannotBeDeleted()
    {
        await _context.CreateBookingService().CreateBookingAsync(1, BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103))));
        _flights.WithBookings.Add(1);
        var service = CreateService();

        var otherAircraft = Edit(_context.Data.Outbound);
        otherAircraft.AircraftId = 20;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(1, otherAircraft));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(1));
    }

    [Fact]
    public async Task StatusChange_NotifiesPassengersWithActiveBookings()
    {
        await _context.CreateBookingService().CreateBookingAsync(1, BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103))));
        _flights.WithBookings.Add(1);

        var delayed = Edit(_context.Data.Outbound);
        delayed.Status = FlightStatus.Delayed;
        var result = await CreateService().UpdateAsync(1, delayed);

        Assert.Equal(FlightStatus.Delayed, result.Status);
        var notification = Assert.Single(await _context.Notifications.GetAllAsync());
        Assert.Equal(NotificationType.FlightUpdate, notification.Type);
        Assert.Equal(1, notification.UserId);
        Assert.Contains("Delayed", notification.Message);
    }

    [Fact]
    public async Task Edit_WithoutAScheduleId_KeepsTheScheduleLinkOfAGeneratedFlight()
    {
        var flight = _context.Data.Return;
        flight.FlightScheduleId = 7;
        var edit = Edit(flight);
        edit.Status = FlightStatus.Delayed;

        await CreateService().UpdateAsync(flight.Id, edit);

        Assert.Equal(7, flight.FlightScheduleId);
        Assert.Equal(FlightStatus.Delayed, flight.Status);
    }

    [Fact]
    public async Task List_IsPaged_AndFilteredByDay_Status_AndSearch()
    {
        var service = CreateService();

        var all = await service.GetPageAsync(null, null, null, 0, 100);
        Assert.Equal(new[] { TestData.OutboundDeparture, TestData.ReturnDeparture }, all.Select(f => f.DepartureAt));

        // take is limited, skip moves on, and the day is the UTC departure day.
        Assert.Single(await service.GetPageAsync(null, null, null, 0, 1));
        Assert.Equal(TestData.ReturnDeparture, Assert.Single(await service.GetPageAsync(null, null, null, 1, 1)).DepartureAt);
        Assert.Empty(await service.GetPageAsync(null, null, null, 2, 10));
        Assert.Equal(TestData.OutboundDeparture, Assert.Single(await service.GetPageAsync(null, null, DateOnly.FromDateTime(TestData.OutboundDeparture), 0, 10)).DepartureAt);
        Assert.Empty(await service.GetPageAsync(null, FlightStatus.Cancelled, null, 0, 10));
        Assert.Equal(2, (await service.GetPageAsync("london", null, null, 0, 10)).Count);
        Assert.Empty(await service.GetPageAsync("paris", null, null, 0, 10));
        Assert.Single(await service.GetPageAsync(null, null, null, -5, 0));
    }

    [Fact]
    public async Task TimeChange_ShiftsSegmentsAndKeepsStops()
    {
        var later = Edit(_context.Data.Outbound);
        later.DepartureAt = later.DepartureAt.AddHours(2);
        later.ArrivalAt = later.ArrivalAt.AddHours(2).AddMinutes(10);

        var result = await CreateService().UpdateAsync(1, later);

        Assert.Equal(1, result.Stops);
        Assert.Equal(new FlightStopDto("IST", 80), result.Via.Single());
        Assert.Equal(545, result.DurationMinutes);
        Assert.Equal(later.DepartureAt, _context.Data.Outbound.Segments.OrderBy(s => s.SequenceNo).First().DepartureAt);
    }

    [Fact]
    public async Task DepartedFlight_CanGetNewStatusButNotAPastDeparture()
    {
        _context.Clock = new FixedTimeProvider(TestData.OutboundDeparture.AddHours(10));
        var service = CreateService();

        var completed = Edit(_context.Data.Outbound);
        completed.Status = FlightStatus.Completed;
        Assert.Equal(FlightStatus.Completed, (await service.UpdateAsync(1, completed)).Status);

        var movedIntoPast = Edit(_context.Data.Outbound);
        movedIntoPast.DepartureAt = movedIntoPast.DepartureAt.AddHours(1);
        movedIntoPast.ArrivalAt = movedIntoPast.ArrivalAt.AddHours(1);
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(1, movedIntoPast));
    }

    [Fact]
    public async Task Delete_FlightWithoutBookings_RemovesIt()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewFlight());

        await service.DeleteAsync(created.Id);

        Assert.Null(await _flights.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task SeatInventory_CountsAndBlocking()
    {
        await _context.CreateBookingService().CreateBookingAsync(1, BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103))));
        _context.Data.Seat(104).Status = SeatStatus.Booked;
        var service = CreateService();

        var inventory = await service.GetSeatInventoryAsync(1);
        Assert.Equal((4, 0, 1, 0), (inventory.Available, inventory.Held, inventory.Booked, inventory.Blocked));

        var blocked = await service.SetSeatBlockedAsync(1, 105, blocked: true);
        Assert.Equal(SeatStatus.Blocked, blocked.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetSeatBlockedAsync(1, 104, blocked: true));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetSeatBlockedAsync(1, 101, blocked: false));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetSeatBlockedAsync(1, 203, blocked: true));

        Assert.Equal(SeatStatus.Available, (await service.SetSeatBlockedAsync(1, 105, blocked: false)).Status);
        Assert.Equal(2, (await service.GetSeatInventoryAsync(1, CabinClass.Business)).Available);
    }
}
