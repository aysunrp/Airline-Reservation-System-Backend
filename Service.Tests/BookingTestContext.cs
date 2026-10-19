using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

/// <summary>Shared in-memory setup for the booking, payment and cancellation tests.</summary>
internal sealed class BookingTestContext
{
    public BookingTestContext()
    {
        Bookings = new FakeBookingRepository(Data)
        {
            PromoCodeLookup = id => PromoCodes.GetByIdAsync(id).Result
        };
    }

    public TestData Data { get; } = new();
    public FixedTimeProvider Clock { get; set; } = new(TestData.Now);
    public FakeBookingRepository Bookings { get; }
    public FakePromoCodeRepository PromoCodes { get; } = new(new PromoCode { Id = 5, Code = "AEROVA10", DiscountPercent = 10, MaxUses = 100, UsedCount = 3 });
    public FakeSavedPassengerRepository SavedPassengers { get; } = new(new SavedPassenger { Id = 7, UserId = 1, FirstName = "Orkhan", LastName = "Aliyev" });
    public FakeNotificationRepository Notifications { get; } = new();

    public FakeFlightSeatRepository FlightSeats() => Data.FlightSeats();

    public SeatService CreateSeatService() => new(Data.Flights(), FlightSeats(), Clock);

    public BookingExpiryService CreateExpiryService() => new(Bookings, CreateSeatService(), Clock);

    public BookingService CreateBookingService() => new(
        Bookings,
        Data.Flights(),
        FlightSeats(),
        new FakeRepository<BaggageOption>(TestData.BaggageOptions),
        new FakeRepository<MealOption>(TestData.MealOptions),
        SavedPassengers,
        new PricingService(Options.Create(new PricingSettings())),
        new PromoCodeService(PromoCodes, Clock),
        CreateExpiryService(),
        new CreateBookingRequestValidator(Clock),
        Options.Create(new BookingSettings { SeatHoldMinutes = 15 }),
        Clock);

    public PaymentService CreatePaymentService() => new(
        Bookings,
        FlightSeats(),
        PromoCodes,
        CreateExpiryService(),
        new NotificationService(Notifications),
        new PaymentRequestValidator(Clock),
        Clock);

    // Same bands as the booking-details page (Economy and Business).
    public FakeCancellationRuleRepository CancellationRules { get; } = new(
        new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 30, FeePercent = 20 },
        new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 14, FeePercent = 40 },
        new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 7, FeePercent = 0 },
        new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 1, FeePercent = 15 });

    public CancellationService CreateCancellationService() => new(
        Bookings,
        new CancellationPolicyService(CancellationRules, Clock),
        CreateSeatService(),
        new NotificationService(Notifications),
        new CancelBookingRequestValidator(),
        Clock);

    public static PaymentRequestDto CardPayment(string pnr) => new()
    {
        Pnr = pnr,
        Method = PaymentMethod.Card,
        CardholderName = "Aysun Panahli",
        CardNumber = "4111 1111 1111 1111",
        ExpiryMonth = 12,
        ExpiryYear = 2028,
        Cvv = "123"
    };

    public static BookingPassengerRequestDto Passenger(string firstName, int birthYear, params (int FlightId, int SeatId)[] seats) => new()
    {
        Title = "Ms",
        FirstName = firstName,
        LastName = "Panahli",
        DateOfBirth = new DateOnly(birthYear, 3, 1),
        Gender = Gender.Female,
        NationalityCode = "AZ",
        DocumentType = DocumentType.Passport,
        DocumentNumber = "C0123456" + firstName.Length,
        DocumentIssuingCountry = "AZ",
        DocumentExpiryDate = new DateOnly(2030, 1, 1),
        Seats = seats.Select(s => new PassengerSeatRequestDto { FlightId = s.FlightId, FlightSeatId = s.SeatId }).ToList()
    };

    public static CreateBookingRequestDto OneWay(params BookingPassengerRequestDto[] passengers) => new()
    {
        TripType = TripType.OneWay,
        Legs = { new BookingLegRequestDto { FlightId = 1, CabinClass = CabinClass.Economy } },
        Passengers = passengers.ToList(),
        ContactEmail = "Aysun@Example.com",
        ContactPhone = "+994 50 834 39 43"
    };
}
