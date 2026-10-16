using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

public class BookingServiceTests
{
    private readonly BookingTestContext _context = new();

    private TestData _data => _context.Data;
    private FakeBookingRepository _bookings => _context.Bookings;

    private BookingService CreateService() => _context.CreateBookingService();

    private static BookingPassengerRequestDto Passenger(string firstName, int birthYear, params (int FlightId, int SeatId)[] seats)
        => BookingTestContext.Passenger(firstName, birthYear, seats);

    private static CreateBookingRequestDto OneWay(params BookingPassengerRequestDto[] passengers)
        => BookingTestContext.OneWay(passengers);

    [Fact]
    public async Task OneWay_WithInfant_HoldsSeatsAndPricesBooking()
    {
        var adult = Passenger("Aysun", 1990, (1, 103));
        adult.BaggageOptionId = 1;
        var secondAdult = Passenger("Orkhan", 1988, (1, 104));
        secondAdult.MealOptionId = 1;
        var infant = Passenger("Nigar", 2025);

        var result = await CreateService().CreateBookingAsync(1, OneWay(adult, secondAdult, infant));

        Assert.Equal(6, result.Pnr.Length);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(TestData.Now.AddMinutes(15), result.ExpiresAt);
        Assert.Equal("aysun@example.com", result.ContactEmail);

        // 365 + 365 + 36.50 (infant 10%), +30 baggage, +18 meal, 10% tax on the fares.
        Assert.Equal(766.50m, result.Price.BaseFare);
        Assert.Equal(30m, result.Price.Baggage);
        Assert.Equal(18m, result.Price.Meals);
        Assert.Equal(76.65m, result.Price.Taxes);
        Assert.Equal(891.15m, result.Price.Total);

        Assert.Equal(new[] { PassengerType.Adult, PassengerType.Adult, PassengerType.Infant }, result.Passengers.Select(p => p.PassengerType));
        Assert.Equal(new[] { "11A", "11B", null }, result.Passengers.Select(p => p.Tickets.Single().Seat));
        Assert.Equal("A350-900", result.Legs.Single().AircraftModel);
        Assert.Equal(new[] { "11A", "11B" }, result.Legs.Single().Seats);
        Assert.Equal("+10 kg", result.Passengers[0].Tickets.Single().BaggageName);
        Assert.Equal("Classic Dining", result.Passengers[1].Tickets.Single().MealName);
        Assert.Null(result.Passengers[2].Tickets.Single().BaggageName);
        Assert.All(result.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Pending, t.Status));
        Assert.Equal("*****4565", result.Passengers[0].DocumentNumberMasked);

        Assert.Equal(SeatStatus.Held, _data.Seat(103).Status);
        Assert.Equal(TestData.Now.AddMinutes(15), _data.Seat(103).HeldUntil);
        Assert.Equal(SeatStatus.Available, _data.Seat(105).Status);
    }

    [Fact]
    public async Task RoundTrip_CreatesTicketPerLegAndChargesBaggageOnce()
    {
        var adult = Passenger("Aysun", 1990, (1, 101), (2, 201));
        adult.BaggageOptionId = 2;
        var request = new CreateBookingRequestDto
        {
            TripType = TripType.RoundTrip,
            Legs =
            {
                new BookingLegRequestDto { FlightId = 1, CabinClass = CabinClass.Business },
                new BookingLegRequestDto { FlightId = 2, CabinClass = CabinClass.Business }
            },
            Passengers = { adult },
            ContactEmail = "a@b.com",
            ContactPhone = "+994501234567"
        };

        var result = await CreateService().CreateBookingAsync(null, request);

        Assert.Equal(new[] { "GYD", "LHR" }, result.Legs.Select(l => l.OriginCode));
        var tickets = result.Passengers.Single().Tickets;
        Assert.Equal(new[] { "1A", "1A" }, tickets.Select(t => t.Seat));
        Assert.Equal(new[] { 50m, 0m }, tickets.Select(t => t.BaggagePrice));
        Assert.Equal(820m + 890m, result.Price.BaseFare);
        Assert.Equal(50m, result.Price.Baggage);
    }

    [Fact]
    public async Task PromoCode_IsAppliedToTheSubtotal()
    {
        var request = OneWay(Passenger("Aysun", 1990, (1, 103)));
        request.PromoCode = "aerova10";

        var result = await CreateService().CreateBookingAsync(null, request);

        Assert.Equal("AEROVA10", result.Price.PromoCode);
        Assert.Equal(401.50m, result.Price.Subtotal);
        Assert.Equal(40.15m, result.Price.Discount);
        Assert.Equal(361.35m, result.Price.Total);
    }

    [Fact]
    public async Task InvalidPromoCode_FailsWithoutSaving()
    {
        var request = OneWay(Passenger("Aysun", 1990, (1, 103)));
        request.PromoCode = "NOPE";

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateBookingAsync(null, request));
        Assert.Empty(await _bookings.GetAllAsync());
    }

    [Fact]
    public async Task PassengerRules_AreEnforced()
    {
        var service = CreateService();

        // An infant cannot have a seat.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null,
            OneWay(Passenger("Aysun", 1990, (1, 103)), Passenger("Nigar", 2025, (1, 104)))));

        // More infants than adults.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null,
            OneWay(Passenger("Aysun", 1990, (1, 103)), Passenger("Nigar", 2025), Passenger("Leyla", 2025))));

        // A child cannot travel without an adult.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null,
            OneWay(Passenger("Kamal", 2018, (1, 103)))));

        // An adult must have a seat on every flight.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null,
            OneWay(Passenger("Aysun", 1990))));
    }

    [Fact]
    public async Task DocumentExpiringBeforeTheTrip_IsRejected()
    {
        var passenger = Passenger("Aysun", 1990, (1, 103));
        passenger.DocumentExpiryDate = new DateOnly(2026, 11, 9);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateBookingAsync(null, OneWay(passenger)));
    }

    [Fact]
    public async Task SeatRules_AreEnforced()
    {
        var service = CreateService();

        // Business seat in an Economy booking.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, OneWay(Passenger("Aysun", 1990, (1, 101)))));

        // Same seat for two passengers.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null,
            OneWay(Passenger("Aysun", 1990, (1, 103)), Passenger("Orkhan", 1988, (1, 103)))));

        // Seat of another flight.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBookingAsync(null, OneWay(Passenger("Aysun", 1990, (1, 203)))));
    }

    [Fact]
    public async Task TakenSeat_ThrowsConflict_ButExpiredHoldCanBeTaken()
    {
        _data.Seat(103).Status = SeatStatus.Booked;
        _data.Seat(104).Status = SeatStatus.Held;
        _data.Seat(104).HeldUntil = TestData.Now.AddMinutes(-1);
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateBookingAsync(null, OneWay(Passenger("Aysun", 1990, (1, 103)))));

        var result = await service.CreateBookingAsync(null, OneWay(Passenger("Aysun", 1990, (1, 104))));
        Assert.Equal("11B", result.Passengers.Single().Tickets.Single().Seat);
    }

    [Fact]
    public async Task SavedPassengerOfAnotherUser_IsNotFound()
    {
        var passenger = Passenger("Orkhan", 1988, (1, 103));
        passenger.SavedPassengerId = 7;

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CreateBookingAsync(2, OneWay(passenger)));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CreateBookingAsync(null, OneWay(passenger)));
    }

    [Fact]
    public async Task InvalidIdCardNumber_ThrowsValidation()
    {
        var passenger = Passenger("Aysun", 1990, (1, 103));
        passenger.DocumentType = DocumentType.NationalIdCard;
        passenger.DocumentNumber = "C01234567";

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateBookingAsync(null, OneWay(passenger)));

        passenger.DocumentNumber = "aa1234567";
        Assert.Equal(6, (await CreateService().CreateBookingAsync(null, OneWay(passenger))).Pnr.Length);
    }

    [Fact]
    public async Task GetByPnr_RequiresMatchingLastName()
    {
        var service = CreateService();
        var created = await service.CreateBookingAsync(1, OneWay(Passenger("Aysun", 1990, (1, 103))));

        Assert.Equal(created.Id, (await service.GetByPnrAsync(created.Pnr.ToLowerInvariant(), " panahli ")).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByPnrAsync(created.Pnr, "Aliyev"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByPnrAsync("ZZZZZZ", "Panahli"));

        var trip = Assert.Single(await service.GetMyBookingsAsync(1));
        Assert.Equal(1, trip.PassengerCount);
        Assert.Null(trip.RefundStatus);
        Assert.Equal(new[] { "11A" }, trip.Legs.Single().Seats);
        Assert.Equal("A350-900", trip.Legs.Single().AircraftModel);
        Assert.Empty(await service.GetMyBookingsAsync(2));
    }

    [Fact]
    public async Task ExpiredHold_ExpiresBookingAndReleasesSeats()
    {
        var service = CreateService();
        var created = await service.CreateBookingAsync(null, OneWay(Passenger("Aysun", 1990, (1, 103))));

        var later = new FixedTimeProvider(TestData.Now.AddMinutes(16));
        var expiry = new BookingExpiryService(_bookings, new SeatService(_data.Flights(), _data.FlightSeats(), later), later);

        Assert.Equal(1, await expiry.ReleaseExpiredAsync());

        var booking = (await _bookings.GetByPnrAsync(created.Pnr))!;
        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.All(booking.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Cancelled, t.Status));
        Assert.Equal(SeatStatus.Available, _data.Seat(103).Status);
        Assert.Null(_data.Seat(103).HeldUntil);

        Assert.Equal(0, await expiry.ReleaseExpiredAsync());
    }
}
