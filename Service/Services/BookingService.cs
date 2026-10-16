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
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class BookingService : IBookingService
{
    // No 0/O or 1/I so references are easy to read out.
    private const string PnrAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TicketNumberLength = 13;
    private const string NotFoundMessage = "We could not find a reservation matching that reference and last name.";

    private readonly IBookingRepository _bookingRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IBaseRepository<BaggageOption> _baggageRepository;
    private readonly IBaseRepository<MealOption> _mealRepository;
    private readonly ISavedPassengerRepository _savedPassengerRepository;
    private readonly IPricingService _pricingService;
    private readonly IPromoCodeService _promoCodeService;
    private readonly IBookingExpiryService _expiryService;
    private readonly IValidator<CreateBookingRequestDto> _validator;
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
        IValidator<CreateBookingRequestDto> validator,
        IOptions<BookingSettings> settings,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _baggageRepository = baggageRepository;
        _mealRepository = mealRepository;
        _savedPassengerRepository = savedPassengerRepository;
        _pricingService = pricingService;
        _promoCodeService = promoCodeService;
        _expiryService = expiryService;
        _validator = validator;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateBookingAsync(int? userId, CreateBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Seats of unpaid bookings whose hold ran out become bookable again before we check availability.
        await _expiryService.ReleaseExpiredAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var holdUntil = now.AddMinutes(_settings.SeatHoldMinutes);

        var legs = await LoadLegsAsync(request, now, cancellationToken);
        var passengerTypes = ResolvePassengerTypes(request, legs);
        await CheckSavedPassengersAsync(userId, request, cancellationToken);
        var seats = await HoldSeatsAsync(request, legs, passengerTypes, now, holdUntil, cancellationToken);

        var baggageOptions = (await _baggageRepository.GetAllAsync(cancellationToken)).Where(o => o.IsActive).ToDictionary(o => o.Id);
        var mealOptions = (await _mealRepository.GetAllAsync(cancellationToken)).Where(o => o.IsActive).ToDictionary(o => o.Id);
        var promoCode = string.IsNullOrWhiteSpace(request.PromoCode)
            ? null
            : await _promoCodeService.ValidateAsync(request.PromoCode, cancellationToken);

        var booking = new Booking
        {
            Pnr = await GeneratePnrAsync(cancellationToken),
            UserId = userId,
            ContactEmail = request.ContactEmail.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone.Replace(" ", string.Empty),
            TripType = request.TripType,
            Status = BookingStatus.Pending,
            ExpiresAt = holdUntil,
            PromoCodeId = promoCode?.Id
        };

        var bookingFlights = legs
            .Select((leg, index) => new BookingFlight { FlightId = leg.Flight.Id, LegOrder = index + 1, CabinClass = leg.Request.CabinClass })
            .ToList();
        foreach (var bookingFlight in bookingFlights)
        {
            booking.Flights.Add(bookingFlight);
        }

        decimal baseFare = 0, baggageTotal = 0, mealTotal = 0;
        for (var p = 0; p < request.Passengers.Count; p++)
        {
            var passengerRequest = request.Passengers[p];
            var baggage = FindOption(baggageOptions, passengerRequest.BaggageOptionId, "baggage");
            var meal = FindOption(mealOptions, passengerRequest.MealOptionId, "meal");

            var passenger = new BookingPassenger
            {
                SavedPassengerId = passengerRequest.SavedPassengerId,
                PassengerType = passengerTypes[p],
                FirstName = passengerRequest.FirstName.Trim(),
                LastName = passengerRequest.LastName.Trim(),
                DateOfBirth = passengerRequest.DateOfBirth,
                Gender = passengerRequest.Gender,
                NationalityCode = passengerRequest.NationalityCode,
                DocumentType = passengerRequest.DocumentType,
                DocumentNumber = passengerRequest.DocumentNumber.Trim().ToUpperInvariant(),
                DocumentIssuingCountry = passengerRequest.DocumentIssuingCountry,
                DocumentExpiryDate = passengerRequest.DocumentExpiryDate
            };

            for (var l = 0; l < legs.Count; l++)
            {
                var fare = _pricingService.GetPassengerFare(legs[l].Fare, passengerTypes[p]);
                seats.TryGetValue((p, legs[l].Flight.Id), out var seat);

                // Baggage and meal are chosen once per passenger for the whole journey; they are charged on the first leg.
                var isFirstLeg = l == 0;
                passenger.Tickets.Add(new Ticket
                {
                    BookingFlight = bookingFlights[l],
                    FlightSeatId = seat?.Id,
                    TicketNumber = RandomNumberGenerator.GetString("0123456789", TicketNumberLength),
                    FareAmount = fare,
                    BaggageOptionId = baggage?.Id,
                    BaggagePrice = isFirstLeg ? baggage?.Price ?? 0 : 0,
                    MealOptionId = meal?.Id,
                    MealPrice = isFirstLeg ? meal?.Price ?? 0 : 0,
                    Status = TicketStatus.Pending
                });
                baseFare += fare;
            }

            baggageTotal += baggage?.Price ?? 0;
            mealTotal += meal?.Price ?? 0;
            booking.Passengers.Add(passenger);
        }

        var price = _pricingService.Calculate(baseFare, baggageTotal, mealTotal, promoCode);
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
            throw new ConflictException("One or more selected seats were just taken. Please choose other seats.");
        }

        return await GetByPnrInternalAsync(booking.Pnr, cancellationToken);
    }

    public async Task<BookingDto> GetByPnrAsync(string pnr, string lastName, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByPnrAsync(NormalizePnr(pnr), cancellationToken);
        var name = lastName?.Trim();

        if (booking is null || string.IsNullOrEmpty(name)
            || !booking.Passengers.Any(p => string.Equals(p.LastName, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return booking.ToDto();
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> GetMyBookingsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetByUserIdAsync(userId, cancellationToken);
        return bookings.Select(b => b.ToSummaryDto()).ToList();
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
                         ?? throw new NotFoundException("Flight not found.");

            if (flight.Status == FlightStatus.Cancelled || flight.DepartureAt <= now)
            {
                throw new BusinessRuleException($"Flight {flight.FlightNumber} can no longer be booked.");
            }

            var fare = flight.Fares.FirstOrDefault(f => f.CabinClass == leg.CabinClass)
                       ?? throw new BusinessRuleException($"{leg.CabinClass} is not offered on flight {flight.FlightNumber}.");

            if (legs.Count > 0 && flight.DepartureAt <= legs[^1].Flight.ArrivalAt)
            {
                throw new BusinessRuleException("Each flight must depart after the previous flight arrives.");
            }

            legs.Add((leg, flight, fare.BasePrice));
        }

        if (request.TripType == TripType.RoundTrip
            && (legs[1].Flight.Route.OriginAirportId != legs[0].Flight.Route.DestinationAirportId
                || legs[1].Flight.Route.DestinationAirportId != legs[0].Flight.Route.OriginAirportId))
        {
            throw new BusinessRuleException("The return flight must fly back on the same route.");
        }

        return legs;
    }

    private List<PassengerType> ResolvePassengerTypes(CreateBookingRequestDto request, List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)> legs)
    {
        // Age is taken on the first travel day; the document must stay valid until the last one.
        var firstTravelDate = DateOnly.FromDateTime(legs[0].Flight.DepartureAt);
        var lastTravelDate = DateOnly.FromDateTime(legs[^1].Flight.DepartureAt);

        var types = request.Passengers.Select(p => _pricingService.GetPassengerType(p.DateOfBirth, firstTravelDate)).ToList();

        var adults = types.Count(t => t == PassengerType.Adult);
        if (adults == 0)
        {
            throw new BusinessRuleException("At least one adult must travel on the booking.");
        }

        if (types.Count(t => t == PassengerType.Infant) > adults)
        {
            throw new BusinessRuleException("Each infant must travel on the lap of an adult, so there cannot be more infants than adults.");
        }

        var expired = request.Passengers.FirstOrDefault(p => p.DocumentExpiryDate is DateOnly expiry && expiry < lastTravelDate);
        if (expired is not null)
        {
            throw new BusinessRuleException($"The travel document of {expired.FirstName} {expired.LastName} expires before the trip ends.");
        }

        return types;
    }

    private async Task CheckSavedPassengersAsync(int? userId, CreateBookingRequestDto request, CancellationToken cancellationToken)
    {
        foreach (var savedPassengerId in request.Passengers.Select(p => p.SavedPassengerId).OfType<int>().Distinct())
        {
            var saved = userId is null ? null : await _savedPassengerRepository.GetByIdAsync(savedPassengerId, cancellationToken);
            if (saved is null || saved.UserId != userId)
            {
                throw new NotFoundException("Saved passenger not found.");
            }
        }
    }

    /// <summary>Checks every seat request and holds the seats. Key: (passenger index, flight id).</summary>
    private async Task<Dictionary<(int Passenger, int FlightId), FlightSeat>> HoldSeatsAsync(
        CreateBookingRequestDto request,
        List<(BookingLegRequestDto Request, Flight Flight, decimal Fare)> legs,
        List<PassengerType> passengerTypes,
        DateTime now,
        DateTime holdUntil,
        CancellationToken cancellationToken)
    {
        var legFlightIds = legs.Select(l => l.Flight.Id).ToHashSet();
        var requested = new Dictionary<(int Passenger, int FlightId), int>();

        for (var p = 0; p < request.Passengers.Count; p++)
        {
            var passenger = request.Passengers[p];
            if (passenger.Seats.Any(s => !legFlightIds.Contains(s.FlightId)))
            {
                throw new BusinessRuleException("A seat was selected on a flight that is not part of this booking.");
            }

            if (passengerTypes[p] == PassengerType.Infant)
            {
                if (passenger.Seats.Count > 0)
                {
                    throw new BusinessRuleException("Infants travel on an adult's lap and cannot have their own seat.");
                }

                continue;
            }

            foreach (var leg in legs)
            {
                var seatsOnFlight = passenger.Seats.Where(s => s.FlightId == leg.Flight.Id).ToList();
                if (seatsOnFlight.Count != 1)
                {
                    throw new BusinessRuleException($"Select one seat for {passenger.FirstName} {passenger.LastName} on flight {leg.Flight.FlightNumber}.");
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
                throw new BusinessRuleException("The same seat cannot be selected for two passengers.");
            }

            var seats = (await _flightSeatRepository.GetTrackedByIdsAsync(leg.Flight.Id, seatIds, cancellationToken)).ToDictionary(s => s.Id);
            if (seats.Count != seatIds.Count)
            {
                throw new BusinessRuleException($"A selected seat does not belong to flight {leg.Flight.FlightNumber}.");
            }

            foreach (var seat in seats.Values)
            {
                var label = SeatService.Label(seat.AircraftSeat);
                if (seat.AircraftSeat.CabinClass != leg.Request.CabinClass)
                {
                    throw new BusinessRuleException($"Seat {label} is not in {leg.Request.CabinClass}.");
                }

                if (!SeatService.IsFree(seat, now))
                {
                    throw new ConflictException($"Seat {label} on flight {leg.Flight.FlightNumber} is no longer available.");
                }

                seat.Status = SeatStatus.Held;
                seat.HeldUntil = holdUntil;
                _flightSeatRepository.Update(seat);
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
            : throw new BusinessRuleException($"The selected {kind} option is not available.");
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
