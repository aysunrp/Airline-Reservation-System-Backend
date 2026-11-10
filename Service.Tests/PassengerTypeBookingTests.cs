using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

/// <summary>
/// Adults, children and infants on one booking: the server works out the type from the date of birth (the request carries no type),
/// on the day of the last flight of the trip by the clock of its departure airport.
/// Flights: outbound 10 Nov 2026 09:00 UTC (Baku), return 17 Nov 2026 15:00 UTC (London). Economy seats 103-105 / 203-205.
/// </summary>
public class PassengerTypeBookingTests
{
    private readonly BookingTestContext _context = new();

    private BookingService CreateService() => _context.CreateBookingService();

    private static BookingPassengerRequestDto Passenger(string firstName, DateOnly dateOfBirth, params (int FlightId, int SeatId)[] seats)
    {
        var passenger = BookingTestContext.Passenger(firstName, dateOfBirth.Year, seats);
        passenger.DateOfBirth = dateOfBirth;
        return passenger;
    }

    private static CreateBookingRequestDto RoundTrip(params BookingPassengerRequestDto[] passengers) => new()
    {
        TripType = TripType.RoundTrip,
        Legs =
        {
            new BookingLegRequestDto { FlightId = 1, CabinClass = CabinClass.Economy },
            new BookingLegRequestDto { FlightId = 2, CabinClass = CabinClass.Economy }
        },
        Passengers = passengers.ToList(),
        ContactEmail = "a@b.com",
        ContactPhone = "+994501234567"
    };

    [Fact]
    public async Task AdultsAndChildren_AreTypedFromTheirDateOfBirthAndPayTheChildFare()
    {
        var adult = Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103));
        var child = Passenger("Kamal", new DateOnly(2018, 3, 1), (1, 104));

        var result = await CreateService().CreateBookingAsync(null, BookingTestContext.OneWay(adult, child));

        Assert.Equal(new[] { PassengerType.Adult, PassengerType.Child }, result.Passengers.Select(p => p.PassengerType));
        Assert.Equal(new[] { "11A", "11B" }, result.Passengers.Select(p => p.Tickets.Single().Seat));
        // The child fare rate is 100%: two full Economy fares, 10% tax on top.
        Assert.Equal(730m, result.Price.BaseFare);
        Assert.Equal(803m, result.Price.Total);
    }

    [Fact]
    public async Task AnInfantOnAnAdultsLap_HasNoSeatAndPaysTenPercent()
    {
        var adult = Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103));
        var infant = Passenger("Nigar", new DateOnly(2026, 1, 15));

        var result = await CreateService().CreateBookingAsync(null, BookingTestContext.OneWay(adult, infant));

        Assert.Equal(PassengerType.Infant, result.Passengers[1].PassengerType);
        Assert.Null(result.Passengers[1].Tickets.Single().Seat);
        Assert.Equal(365m + 36.50m, result.Price.BaseFare);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(104).Status);
    }

    [Fact]
    public async Task AnInfantFare_IsPaidForEveryFlightOfTheTrip()
    {
        var adult = Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103), (2, 203));
        var infant = Passenger("Nigar", new DateOnly(2026, 1, 15));

        var result = await CreateService().CreateBookingAsync(null, RoundTrip(adult, infant));

        // Adult: 365 + 420. Infant: 10% of each flight's fare = 36.50 + 42.
        Assert.Equal(365m + 420m + 36.50m + 42m, result.Price.BaseFare);
        Assert.All(result.Passengers[1].Tickets, ticket => Assert.Null(ticket.Seat));
    }

    [Fact]
    public async Task TheAge_IsCountedOnTheLastFlight_SoAnInfantWhoTurnsTwoBeforeTheReturnIsAChild()
    {
        // Born 12 Nov 2024: still under 2 on the outbound flight (10 Nov), exactly 2 on the return flight (17 Nov).
        var born = new DateOnly(2024, 11, 12);

        // One way, outbound only: an infant, so no seat may be chosen for the passenger.
        var oneWay = await CreateService().CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Nigar", born)));
        Assert.Equal(PassengerType.Infant, oneWay.Passengers[1].PassengerType);

        // Round trip: the child needs a seat on both flights and pays the full fare for both.
        var context = new BookingTestContext();
        var roundTrip = await context.CreateBookingService().CreateBookingAsync(null, RoundTrip(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103), (2, 203)),
            Passenger("Nigar", born, (1, 104), (2, 204))));
        Assert.Equal(PassengerType.Child, roundTrip.Passengers[1].PassengerType);
        Assert.Equal(2 * (365m + 420m), roundTrip.Price.BaseFare);

        // Without seats the same passenger is refused: a child cannot travel on a lap.
        var withoutSeats = new BookingTestContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => withoutSeats.CreateBookingService().CreateBookingAsync(null, RoundTrip(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103), (2, 203)),
            Passenger("Nigar", born))));
    }

    [Fact]
    public async Task TheAge_IsCountedOnTheLocalDayOfTheDepartureAirport_NotTheUtcDay()
    {
        // Departure 10 Nov 21:30 UTC is 11 Nov 01:30 in Baku (UTC+4). Born 11 Nov 2024: two years old by the Baku clock.
        _context.Data.Outbound.DepartureAt = new DateTime(2026, 11, 10, 21, 30, 0, DateTimeKind.Utc);
        _context.Data.Outbound.ArrivalAt = new DateTime(2026, 11, 11, 6, 0, 0, DateTimeKind.Utc);

        var result = await CreateService().CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Nigar", new DateOnly(2024, 11, 11), (1, 104))));

        Assert.Equal(PassengerType.Child, result.Passengers[1].PassengerType);
    }

    [Fact]
    public async Task APassengerCountMismatch_BetweenSeatsAndTypes_IsRefused()
    {
        var service = CreateService();

        // An adult without a seat.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1)))));

        // A child without a seat.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Kamal", new DateOnly(2018, 3, 1)))));

        // An infant with a seat.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Nigar", new DateOnly(2026, 1, 15), (1, 104)))));

        // Two passengers on one seat.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Kamal", new DateOnly(2018, 3, 1), (1, 103)))));

        // Nothing was held by the refused bookings.
        Assert.All(new[] { 103, 104, 105 }, id => Assert.Equal(SeatStatus.Available, _context.Data.Seat(id).Status));
        Assert.Empty(await _context.Bookings.GetAllAsync());
    }

    [Fact]
    public async Task EveryInfantNeedsAnAdult_AndAChildIsNotAnAdult()
    {
        var service = CreateService();

        // Two infants, one adult.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Nigar", new DateOnly(2026, 1, 15)),
            Passenger("Leyla", new DateOnly(2026, 2, 15)))));

        // A child with an infant and no adult.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Kamal", new DateOnly(2018, 3, 1), (1, 103)),
            Passenger("Nigar", new DateOnly(2026, 1, 15)))));

        // Two adults with two infants and a child is fine.
        var result = await service.CreateBookingAsync(null, BookingTestContext.OneWay(
            Passenger("Aysun", new DateOnly(1990, 3, 1), (1, 103)),
            Passenger("Orkhan", new DateOnly(1988, 3, 1), (1, 104)),
            Passenger("Kamal", new DateOnly(2018, 3, 1), (1, 105)),
            Passenger("Nigar", new DateOnly(2026, 1, 15)),
            Passenger("Leyla", new DateOnly(2026, 2, 15))));

        Assert.Equal(
            new[] { PassengerType.Adult, PassengerType.Adult, PassengerType.Child, PassengerType.Infant, PassengerType.Infant },
            result.Passengers.Select(p => p.PassengerType));
    }

    [Fact]
    public void ABookingHasAtMostNinePassengers_InfantsIncluded()
    {
        var validator = new CreateBookingRequestValidator(_context.Clock);
        var passengers = Enumerable.Range(0, 10).Select(i => Passenger("P" + i, new DateOnly(1990, 3, 1), (1, 100 + i))).ToList();

        var nine = BookingTestContext.OneWay();
        nine.Passengers = passengers.Take(9).ToList();
        Assert.True(validator.Validate(nine).IsValid);

        var ten = BookingTestContext.OneWay();
        ten.Passengers = passengers;
        Assert.Contains(validator.Validate(ten).Errors, e => e.ErrorMessage == "A booking can have at most 9 passengers.");
    }

    [Fact]
    public async Task ADateOfBirthInTheFuture_IsRefusedByTheServer()
    {
        var future = Passenger("Aysun", new DateOnly(2030, 1, 1), (1, 103));

        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateBookingAsync(null, BookingTestContext.OneWay(future)));

        Assert.Contains(error.Errors, e => e.ErrorMessage == "Enter a valid date of birth in the past.");
    }

    [Fact]
    public void ThePassengerRules_ShowTheLimitsTheServerApplies()
    {
        var rules = new PricingService(Options.Create(new PricingSettings { InfantMaxAgeYears = 3, ChildMaxAgeYears = 14 })).GetPassengerRules();

        Assert.Equal(new PassengerRulesDto(3, 14, 9), rules);
        Assert.Equal(new PassengerRulesDto(2, 12, 9), new PricingService(Options.Create(new PricingSettings())).GetPassengerRules());
    }
}
