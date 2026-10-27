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
        Tokens = new FakeEmailTokenRepository(Users);
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

    /// <summary>The accounts bookings are made for; empty unless a test needs the e-mail check.</summary>
    public FakeUserRepository Users { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    public FakeEmailTokenRepository Tokens { get; }

    /// <summary>Off here so the many booking tests need no verified account; the tests of that rule switch it on.</summary>
    public AuthSettings AuthSettings { get; } = new()
    {
        RequireVerifiedEmailForBooking = false,
        RequireSignInToBook = false,
        RequireBookingEmailConfirmation = false,
        FrontendBaseUrl = "https://aerova.test"
    };

    public BookingEmailConfirmationService CreateBookingEmailConfirmation() => new(
        Bookings,
        Tokens,
        EmailSender,
        Options.Create(AuthSettings),
        Clock,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<BookingEmailConfirmationService>.Instance);

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
        new TravelDocumentPolicy(Options.Create(TestData.FrontendDocumentRules())),
        new CreateBookingRequestValidator(Clock),
        Options.Create(new BookingSettings { SeatHoldMinutes = 15 }),
        Clock,
        Users,
        EmailSender,
        Options.Create(AuthSettings),
        CreateBookingEmailConfirmation());

    /// <summary>Stripe is not configured unless a test sets the two secrets.</summary>
    public StripeSettings Stripe { get; } = new();

    /// <summary>The pretend payment stays usable here, as in Development.</summary>
    public PaymentSettings PaymentSettings { get; } = new() { AllowMockPayments = true };

    public FakeStripeGateway Gateway { get; } = new();

    public BookingConfirmationService CreateConfirmationService() => new(Bookings, FlightSeats(), new NotificationService(Notifications));

    public PaymentService CreatePaymentService() => new(
        Bookings,
        PromoCodes,
        CreateExpiryService(),
        CreateConfirmationService(),
        new PaymentRequestValidator(Clock),
        Clock,
        Options.Create(Stripe),
        Options.Create(PaymentSettings));

    public FakeStripeWebhookEventRepository WebhookEvents { get; } = new();

    public StripeWebhookService CreateWebhookService() => new(
        WebhookEvents,
        new FakePaymentRepository(Bookings),
        Bookings,
        PromoCodes,
        Refunds,
        CreateRefundService(),
        CreateConfirmationService(),
        Options.Create(Stripe),
        Clock,
        new ListLogger<StripeWebhookService>());

    public CheckoutService CreateCheckoutService() => new(
        Bookings,
        new FakePaymentRepository(Bookings),
        PromoCodes,
        CreateExpiryService(),
        Gateway,
        new CreateCheckoutSessionRequestValidator(),
        Options.Create(Stripe),
        Options.Create(new AuthSettings { FrontendBaseUrl = "https://aerova.test/", RequireBookingEmailConfirmation = AuthSettings.RequireBookingEmailConfirmation }),
        Clock);

    // Same bands as the booking-details page (Economy and Business).
    public FakeCancellationRuleRepository CancellationRules { get; } = new(
        new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 30, FeePercent = 20 },
        new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 14, FeePercent = 40 },
        new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 7, FeePercent = 0 },
        new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 1, FeePercent = 15 });

    private FakeRefundRepository? _refunds;

    public FakeRefundRepository Refunds => _refunds ??= new FakeRefundRepository(Bookings);

    public FakeStripeRefundGateway RefundGateway { get; } = new();

    public FakeAuditLogRepository AuditEntries { get; } = new();

    public RefundService CreateRefundService() => new(
        Refunds,
        RefundGateway,
        new NotificationService(Notifications),
        new AuditLogService(AuditEntries, new ListLogger<AuditLogService>()),
        Clock,
        new ListLogger<RefundService>());

    public CancellationService CreateCancellationService() => new(
        Bookings,
        new CancellationPolicyService(CancellationRules, Clock),
        CreateSeatService(),
        new NotificationService(Notifications),
        CreateRefundService(),
        new CancelBookingRequestValidator(),
        Clock,
        new ListLogger<CancellationService>());

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
