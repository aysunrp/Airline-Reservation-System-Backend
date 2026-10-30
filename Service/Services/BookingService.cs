using System.Security.Cryptography;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Scheduling;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class BookingService : IBookingService
{
    // No 0/O or 1/I so references are easy to read out.
    private const string PnrAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TicketNumberLength = 13;
    private const string NotFoundMessage = "Bu kod və soyada uyğun rezervasiya tapılmadı.";

    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly AuthSettings _authSettings;
    private readonly IBookingRepository _bookingRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IBaseRepository<BaggageOption> _baggageRepository;
    private readonly IBaseRepository<MealOption> _mealRepository;
    private readonly ISavedPassengerRepository _savedPassengerRepository;
    private readonly IPricingService _pricingService;
    private readonly IPromoCodeService _promoCodeService;
    private readonly IBookingExpiryService _expiryService;
    private readonly ITravelDocumentPolicy _documentPolicy;
    private readonly IValidator<CreateBookingRequestDto> _validator;
    private readonly IBookingEmailConfirmationService _confirmation;
    private readonly IAdminAlertService? _adminAlerts;
    private readonly BookingSettings _settings;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IBookingRepository bookingRepository,
        IFlightRepository flightRepository,
        IFlightSeatRepository flightSeatRepository,
        IBaseRepository<BaggageOption> baggageRepository,
        IBaseRepository<MealOption> mealRepository,
        ISavedPassengerRepository savedPassengerRepository,
        IPricingService pricingService,
        IPromoCodeService promoCodeService,
        IBookingExpiryService expiryService,
        ITravelDocumentPolicy documentPolicy,
        IValidator<CreateBookingRequestDto> validator,
        IOptions<BookingSettings> settings,
        TimeProvider timeProvider,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IOptions<AuthSettings> authSettings,
        IBookingEmailConfirmationService confirmation,
        IAdminAlertService? adminAlerts = null)
    {
        _adminAlerts = adminAlerts;
        _confirmation = confirmation;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _authSettings = authSettings.Value;
        _bookingRepository = bookingRepository;
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _baggageRepository = baggageRepository;
        _mealRepository = mealRepository;
        _savedPassengerRepository = savedPassengerRepository;
        _pricingService = pricingService;
        _promoCodeService = promoCodeService;
        _expiryService = expiryService;
        _documentPolicy = documentPolicy;
        _validator = validator;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateBookingAsync(int? userId, CreateBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        // Before anything is read or held: a guest gets no seats and no booking.
        if (userId is null && _authSettings.RequireSignInToBook)
        {
            throw new UnauthorizedException("Uçuşu bron etmək üçün daxil olun.");
        }

        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        await RequireVerifiedEmailAsync(userId, cancellationToken);

        // Seats of unpaid bookings whose hold ran out become bookable again before we check availability.
        await _expiryService.ReleaseExpiredAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        // How long the unpaid booking waits for its payment; no seat is held meanwhile.
        var holdUntil = now.AddMinutes(_settings.SeatHoldMinutes);

        var quote = await PrepareAsync(request, now, cancellationToken);
        await CheckSavedPassengersAsync(userId, request, cancellationToken);
        // Seats are only checked here: choosing one reserves nothing. They are claimed when the payment is confirmed.
        var seats = await CheckSeatsAsync(request, quote.Legs, quote.PassengerTypes, now, cancellationToken);

        var booking = new Booking
        {
            Pnr = await GeneratePnrAsync(cancellationToken),
            UserId = userId,
            ContactEmail = request.ContactEmail.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone.Replace(" ", string.Empty),
            TripType = request.TripType,
            Status = BookingStatus.Pending,
            ExpiresAt = holdUntil,
            PromoCodeId = quote.PromoCode?.Id,
            // A booking of an account waits for its own e-mail link; one that needs none is confirmed from the start.
            ContactConfirmedAt = userId is not null && _confirmation.IsRequired ? null : now
        };

        var bookingFlights = quote.Legs
            .Select((leg, index) => new BookingFlight { FlightId = leg.Flight.Id, LegOrder = index + 1, CabinClass = leg.Request.CabinClass })
            .ToList();
        foreach (var bookingFlight in bookingFlights)
        {
            booking.Flights.Add(bookingFlight);
        }

        for (var p = 0; p < request.Passengers.Count; p++)
        {
            var passengerRequest = request.Passengers[p];
            var baggage = quote.Baggage[p];
            var meal = quote.Meals[p];

            var passenger = new BookingPassenger
            {
                SavedPassengerId = passengerRequest.SavedPassengerId,
                PassengerType = quote.PassengerTypes[p],
                Title = passengerRequest.Title,
                FirstName = passengerRequest.FirstName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(passengerRequest.MiddleName) ? null : passengerRequest.MiddleName.Trim(),
                LastName = passengerRequest.LastName.Trim(),
                DateOfBirth = passengerRequest.DateOfBirth,
                Gender = passengerRequest.Gender,
                NationalityCode = passengerRequest.NationalityCode,
                DocumentType = passengerRequest.DocumentType,
                DocumentNumber = passengerRequest.DocumentNumber.Trim().ToUpperInvariant(),
                DocumentIssuingCountry = passengerRequest.DocumentIssuingCountry,
                DocumentExpiryDate = passengerRequest.DocumentExpiryDate
            };

            for (var l = 0; l < quote.Legs.Count; l++)
            {
                seats.TryGetValue((p, quote.Legs[l].Flight.Id), out var seat);

                // Baggage and meal are chosen once per passenger for the whole journey; they are charged on the first leg.
                var isFirstLeg = l == 0;
                passenger.Tickets.Add(new Ticket
                {
                    BookingFlight = bookingFlights[l],
                    FlightSeatId = seat?.Id,
                    TicketNumber = RandomNumberGenerator.GetString("0123456789", TicketNumberLength),
                    FareAmount = _pricingService.GetPassengerFare(quote.Legs[l].Fare, quote.PassengerTypes[p]),
                    BaggageOptionId = baggage?.Id,
                    BaggagePrice = isFirstLeg ? baggage?.Price ?? 0 : 0,
                    MealOptionId = meal?.Id,
                    MealPrice = isFirstLeg ? meal?.Price ?? 0 : 0,
                    Status = TicketStatus.Pending
                });
            }

            booking.Passengers.Add(passenger);
        }

        var price = quote.Price;
        booking.BaseFare = price.BaseFare;
        booking.BaggageTotal = price.Baggage;
        booking.MealTotal = price.Meals;
        booking.TaxesTotal = price.Taxes;
        booking.DiscountAmount = price.Discount;
        booking.TotalAmount = price.Total;

        await _bookingRepository.AddAsync(booking, cancellationToken);
        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another booking changed one of these seats in the meantime (row version) or already holds an
            // active ticket for it (unique index). Nothing was saved.
            throw new ConflictException("Seçilmiş oturacaqlardan bir və ya bir neçəsi indicə tutuldu. Başqa oturacaqlar seçin.");
        }

        if (_adminAlerts is not null)
        {
            await _adminAlerts.QueueAndSaveAsync($"booking:{booking.Id}:created", "New booking created",
                $"Booking {booking.Pnr} was created for {booking.Passengers.Count} passenger(s), total {booking.TotalAmount:0.00} {Domain.Constants.AppConstants.Currency}. Payment is still pending.\n\nBooking id: {booking.Id}", cancellationToken);
        }

        if (booking.ContactConfirmedAt is null)
        {
            // Best effort: when the message does not go out the page offers to send it again.
            await _confirmation.SendAsync(booking, request.Language, cancellationToken);
        }

        return await GetByPnrInternalAsync(booking.Pnr, cancellationToken);
    }

    public async Task<PriceBreakdownDto> QuoteAsync(CreateBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var quote = await PrepareAsync(request, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return quote.Price;
    }

    public async Task<BookingDto> GetByPnrAsync(string pnr, string lastName, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByPnrAsync(NormalizePnr(pnr), cancellationToken);
        if (booking is null || !HasPassengerNamed(booking, lastName))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return booking.ToDto();
    }

    internal const string BookingNotFoundMessage = NotFoundMessage;

    /// <summary>Manage-booking access check: one of the passengers must have this last name.</summary>
    internal static bool HasPassengerNamed(Booking booking, string? lastName)
    {
        var name = lastName?.Trim();
        return !string.IsNullOrEmpty(name)
               && booking.Passengers.Any(p => string.Equals(p.LastName, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> GetMyBookingsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetByUserIdAsync(userId, cancellationToken);
        return bookings.Select(b => b.ToSummaryDto()).ToList();
    }

    public async Task<BookingDto> GetMyBookingAsync(int userId, string pnr, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByPnrAsync(NormalizePnr(pnr), cancellationToken);

        // Someone else's booking is reported exactly like a missing one.
        if (booking is null || booking.UserId != userId)
        {
            throw new NotFoundException("Bron tapılmadı.");
        }

        return booking.ToDto();
    }

    internal static string NormalizePnr(string pnr) => (pnr ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<BookingDto> GetByPnrInternalAsync(string pnr, CancellationToken cancellationToken)
        => (await _bookingRepository.GetByPnrAsync(pnr, cancellationToken))?.ToDto()
           ?? throw new NotFoundException(NotFoundMessage);

    private async Task<List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)>> LoadLegsAsync(
        CreateBookingRequestDto request, DateTime now, CancellationToken cancellationToken)
    {
        var legs = new List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)>();
        foreach (var leg in request.Legs)
        {
            var flight = await _flightRepository.GetDetailsAsync(leg.FlightId, cancellationToken)
                         ?? throw new NotFoundException("Uçuş tapılmadı.");

            if (flight.Status == FlightStatus.Cancelled || flight.DepartureAt <= now)
            {
                throw new BusinessRuleException($"{flight.FlightNumber} nömrəli uçuşu artıq bron etmək mümkün deyil.");
            }

            var fare = flight.Fares.FirstOrDefault(f => f.CabinClass == leg.CabinClass)
                       ?? throw new BusinessRuleException($"{flight.FlightNumber} nömrəli uçuşda {leg.CabinClass} sinfi təklif olunmur.");

            if (legs.Count > 0 && flight.DepartureAt <= legs[^1].Flight.ArrivalAt)
            {
                throw new BusinessRuleException("Hər uçuş əvvəlki uçuş enəndən sonra yola düşməlidir.");
            }

            legs.Add((leg, flight, fare.BasePrice));
        }

        if (request.TripType == TripType.RoundTrip
            && (legs[1].Flight.Route.OriginAirportId != legs[0].Flight.Route.DestinationAirportId
                || legs[1].Flight.Route.DestinationAirportId != legs[0].Flight.Route.OriginAirportId))
        {
            throw new BusinessRuleException("Qayıdış uçuşu eyni marşrutla geri uçmalıdır.");
        }

        return legs;
    }

    /// <summary>Everything the price depends on, resolved once and shared by quoting and booking.</summary>
    private sealed record BookingQuote(
        List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)> Legs,
        List<PassengerType> PassengerTypes,
        List<BaggageOption?> Baggage,
        List<MealOption?> Meals,
        PromoCodeDto? PromoCode,
        PriceBreakdownDto Price);

    /// <summary>Checks flights, passengers, add-ons and the promo code, and prices the booking. Seats are not checked or held.</summary>
    private async Task<BookingQuote> PrepareAsync(CreateBookingRequestDto request, DateTime now, CancellationToken cancellationToken)
    {
        var legs = await LoadLegsAsync(request, now, cancellationToken);
        var passengerTypes = ResolvePassengerTypes(request, legs);

        var baggageOptions = (await _baggageRepository.GetAllAsync(cancellationToken)).Where(o => o.IsActive).ToDictionary(o => o.Id);
        var mealOptions = (await _mealRepository.GetAllAsync(cancellationToken)).Where(o => o.IsActive).ToDictionary(o => o.Id);
        var baggage = request.Passengers.Select(p => FindOption(baggageOptions, p.BaggageOptionId, "baggage")).ToList();
        var meals = request.Passengers.Select(p => FindOption(mealOptions, p.MealOptionId, "meal")).ToList();

        var promoCode = string.IsNullOrWhiteSpace(request.PromoCode)
            ? null
            : await _promoCodeService.ValidateAsync(request.PromoCode, cancellationToken);

        var baseFare = passengerTypes.Sum(type => legs.Sum(leg => _pricingService.GetPassengerFare(leg.Fare, type)));
        var price = _pricingService.Calculate(
            baseFare,
            baggage.Sum(o => o?.Price ?? 0),
            meals.Sum(o => o?.Price ?? 0),
            promoCode);

        return new BookingQuote(legs, passengerTypes, baggage, meals, promoCode, price);
    }

    private List<PassengerType> ResolvePassengerTypes(CreateBookingRequestDto request, List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)> legs)
    {
        // Age is taken on the day of the last flight of the trip, by the clock of its departure airport: a child who turns 12 (or an
        // infant who turns 2) before the return flight is priced and seated as the older passenger on the whole booking.
        // The document must stay valid until that same day.
        var lastFlight = legs[^1].Flight;
        var lastTravelDate = ScheduleTimeCalculator.LocalDate(lastFlight.DepartureAt, lastFlight.Route.Origin);

        var types = request.Passengers.Select(p => _pricingService.GetPassengerType(p.DateOfBirth, lastTravelDate)).ToList();

        var adults = types.Count(t => t == PassengerType.Adult);
        if (adults == 0)
        {
            throw new BusinessRuleException("Bronda ən azı bir böyük sərnişin olmalıdır.");
        }

        if (types.Count(t => t == PassengerType.Infant) > adults)
        {
            throw new BusinessRuleException("Hər körpə böyüyün qucağında səyahət etməlidir, ona görə körpələrin sayı böyüklərin sayından çox ola bilməz.");
        }

        // Accepted document types and minimum validity depend on the countries of every flight.
        _documentPolicy.Check(legs.Select(l => l.Flight).ToList(), request.Passengers, lastTravelDate);

        return types;
    }

    /// <summary>
    /// A signed-in customer needs a confirmed e-mail address to book. The rule only applies while e-mail can be sent at all: without
    /// SMTP nobody could confirm an address, and the rule would lock every customer out. Guests and admins are not affected.
    /// </summary>
    private async Task RequireVerifiedEmailAsync(int? userId, CancellationToken cancellationToken)
    {
        if (userId is null || !_authSettings.RequireVerifiedEmailForBooking || !_emailSender.IsConfigured)
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is not null && user.Role == UserRole.User && user.EmailVerifiedAt is null)
        {
            throw new BusinessRuleException("Bron etməzdən əvvəl e-poçt ünvanınızı təsdiqləyin. Göndərdiyimiz keçidi açın və ya profilinizdən yenisini istəyin.");
        }
    }

    private async Task CheckSavedPassengersAsync(int? userId, CreateBookingRequestDto request, CancellationToken cancellationToken)
    {
        foreach (var savedPassengerId in request.Passengers.Select(p => p.SavedPassengerId).OfType<int>().Distinct())
        {
            var saved = userId is null ? null : await _savedPassengerRepository.GetByIdAsync(savedPassengerId, cancellationToken);
            if (saved is null || saved.UserId != userId)
            {
                throw new NotFoundException("Saxlanılmış sərnişin tapılmadı.");
            }
        }
    }

    /// <summary>Checks every seat request (the seat exists on the flight, is in the cabin and is free right now). Holds nothing. Key: (passenger index, flight id).</summary>
    private async Task<Dictionary<(int Passenger, int FlightId), FlightSeat>> CheckSeatsAsync(
        CreateBookingRequestDto request,
        List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)> legs,
        List<PassengerType> passengerTypes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var legFlightIds = legs.Select(l => l.Flight.Id).ToHashSet();
        var requested = new Dictionary<(int Passenger, int FlightId), int>();

        for (var p = 0; p < request.Passengers.Count; p++)
        {
            var passenger = request.Passengers[p];
            if (passenger.Seats.Any(s => !legFlightIds.Contains(s.FlightId)))
            {
                throw new BusinessRuleException("Bu bronun tərkibində olmayan uçuş üçün oturacaq seçilib.");
            }

            if (passengerTypes[p] == PassengerType.Infant)
            {
                if (passenger.Seats.Count > 0)
                {
                    throw new BusinessRuleException("Körpələr böyüyün qucağında səyahət edir və ayrıca oturacaq seçə bilməz.");
                }

                continue;
            }

            foreach (var leg in legs)
            {
                var seatsOnFlight = passenger.Seats.Where(s => s.FlightId == leg.Flight.Id).ToList();
                if (seatsOnFlight.Count != 1)
                {
                    throw new BusinessRuleException($"{leg.Flight.FlightNumber} nömrəli uçuşda {passenger.FirstName} {passenger.LastName} üçün bir oturacaq seçin.");
                }

                requested[(p, leg.Flight.Id)] = seatsOnFlight[0].FlightSeatId;
            }
        }

        var held = new Dictionary<(int Passenger, int FlightId), FlightSeat>();
        foreach (var leg in legs)
        {
            var seatIds = requested.Where(r => r.Key.FlightId == leg.Flight.Id).Select(r => r.Value).ToList();
            if (seatIds.Distinct().Count() != seatIds.Count)
            {
                throw new BusinessRuleException("Eyni oturacaq iki sərnişin üçün seçilə bilməz.");
            }

            var seats = (await _flightSeatRepository.GetTrackedByIdsAsync(leg.Flight.Id, seatIds, cancellationToken)).ToDictionary(s => s.Id);
            if (seats.Count != seatIds.Count)
            {
                throw new BusinessRuleException($"Seçilmiş oturacaq {leg.Flight.FlightNumber} nömrəli uçuşa aid deyil.");
            }

            foreach (var seat in seats.Values)
            {
                var label = SeatService.Label(seat.AircraftSeat);
                if (seat.AircraftSeat.CabinClass != leg.Request.CabinClass)
                {
                    throw new BusinessRuleException($"{label} oturacağı {leg.Request.CabinClass} sinfində deyil.");
                }

                if (!SeatService.IsFree(seat, now))
                {
                    throw new ConflictException($"{leg.Flight.FlightNumber} nömrəli uçuşda {label} oturacağı artıq əlçatan deyil.");
                }

            }

            foreach (var entry in requested.Where(r => r.Key.FlightId == leg.Flight.Id))
            {
                held[entry.Key] = seats[entry.Value];
            }
        }

        return held;
    }

    private static T? FindOption<T>(IReadOnlyDictionary<int, T> options, int? id, string kind) where T : class
    {
        if (id is null)
        {
            return null;
        }

        return options.TryGetValue(id.Value, out var option)
            ? option
            : throw new BusinessRuleException($"Seçilmiş {kind} variantı əlçatan deyil.");
    }

    private async Task<string> GeneratePnrAsync(CancellationToken cancellationToken)
    {
        // Collisions are very unlikely (32^6 combinations); retry a few times just in case.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var pnr = RandomNumberGenerator.GetString(PnrAlphabet, AppConstants.PnrLength);
            if (!await _bookingRepository.PnrExistsAsync(pnr, cancellationToken))
            {
                return pnr;
            }
        }

        throw new InvalidOperationException("Could not generate a unique booking reference.");
    }
}
