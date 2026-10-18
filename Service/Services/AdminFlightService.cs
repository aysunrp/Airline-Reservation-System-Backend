using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seat;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Validators;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminFlightService : IAdminFlightService
{
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IAirportRepository _airportRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationService _notificationService;
    private readonly IValidator<FlightRequestDto> _validator;
    private readonly TimeProvider _timeProvider;

    public AdminFlightService(
        IFlightRepository flightRepository,
        IFlightSeatRepository flightSeatRepository,
        IRouteRepository routeRepository,
        IAirportRepository airportRepository,
        IAircraftRepository aircraftRepository,
        IFlightScheduleRepository scheduleRepository,
        IBookingRepository bookingRepository,
        INotificationService notificationService,
        IValidator<FlightRequestDto> validator,
        TimeProvider timeProvider)
    {
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _routeRepository = routeRepository;
        _airportRepository = airportRepository;
        _aircraftRepository = aircraftRepository;
        _scheduleRepository = scheduleRepository;
        _bookingRepository = bookingRepository;
        _notificationService = notificationService;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<FlightResultDto>> GetAllAsync(string? search = null, FlightStatus? status = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        var flights = await _flightRepository.GetAllWithDetailsAsync(cancellationToken);

        return flights
            .Select(f => f.ToResultDto())
            .Where(f => status is null || f.Status == status)
            .Where(f => string.IsNullOrEmpty(term)
                        || f.FlightNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || f.OriginCity.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || f.DestinationCity.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || f.AircraftModel.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<FlightResultDto> CreateAsync(FlightRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var flightNumber = FlightNumber.Normalize(request.FlightNumber);
        var departureAt = AsUtc(request.DepartureAt);
        var arrivalAt = AsUtc(request.ArrivalAt);
        EnsureDepartureInFuture(departureAt);
        await EnsureUniqueAsync(flightNumber, departureAt, null, cancellationToken);

        var route = await GetActiveRouteAsync(request, cancellationToken);
        var aircraft = await GetUsableAircraftAsync(request.AircraftId, cancellationToken);
        CheckFaresMatchAircraft(request.Fares.Keys, aircraft);
        await CheckScheduleAsync(request.FlightScheduleId, cancellationToken);

        var flight = new Flight
        {
            FlightNumber = flightNumber,
            FlightScheduleId = request.FlightScheduleId,
            RouteId = route.Id,
            AircraftId = aircraft.Id,
            DepartureAt = departureAt,
            ArrivalAt = arrivalAt,
            Status = request.Status
        };
        flight.Segments.Add(DirectSegment(route, departureAt, arrivalAt));
        foreach (var (cabin, price) in request.Fares)
        {
            flight.Fares.Add(new FlightFare { CabinClass = cabin, BasePrice = price });
        }

        AddSeatInventory(flight, aircraft);

        await _flightRepository.AddAsync(flight, cancellationToken);
        await SaveAsync(cancellationToken);

        return await GetResultAsync(flight.Id, cancellationToken);
    }

    public async Task<FlightResultDto> UpdateAsync(int id, FlightRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var flight = await _flightRepository.GetForUpdateAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Flight not found.");

        var flightNumber = FlightNumber.Normalize(request.FlightNumber);
        var departureAt = AsUtc(request.DepartureAt);
        var arrivalAt = AsUtc(request.ArrivalAt);
        await EnsureUniqueAsync(flightNumber, departureAt, id, cancellationToken);

        var route = await GetActiveRouteAsync(request, cancellationToken);
        var hasBookings = await _flightRepository.HasBookingsAsync(id, cancellationToken);
        var routeChanged = route.Id != flight.RouteId;
        var aircraftChanged = request.AircraftId != flight.AircraftId;

        if (hasBookings && (routeChanged || aircraftChanged))
        {
            throw new BusinessRuleException("The route and aircraft of a flight with bookings cannot be changed.");
        }

        var aircraft = aircraftChanged
            ? await GetUsableAircraftAsync(request.AircraftId, cancellationToken)
            : await _aircraftRepository.GetWithSeatsAsync(flight.AircraftId, cancellationToken) ?? throw new NotFoundException("Aircraft not found.");
        CheckFaresMatchAircraft(request.Fares.Keys, aircraft);

        if (hasBookings && flight.Fares.Any(f => !request.Fares.ContainsKey(f.CabinClass)))
        {
            throw new BusinessRuleException("A cabin that is already sold on this flight cannot be removed.");
        }

        await CheckScheduleAsync(request.FlightScheduleId, cancellationToken);

        var statusChanged = flight.Status != request.Status;
        var timesChanged = flight.DepartureAt != departureAt || flight.ArrivalAt != arrivalAt;

        // A flight that already left can still get a new status (e.g. Completed), but cannot be moved into the past.
        if (flight.DepartureAt != departureAt)
        {
            EnsureDepartureInFuture(departureAt);
        }

        if (routeChanged || flight.Segments.Count == 0)
        {
            // A flight moved to another route becomes a direct flight on that route.
            flight.Segments.Clear();
            flight.Segments.Add(DirectSegment(route, departureAt, arrivalAt));
        }
        else if (timesChanged)
        {
            ShiftSegments(flight, departureAt, arrivalAt);
        }

        if (aircraftChanged)
        {
            flight.Seats.Clear();
            AddSeatInventory(flight, aircraft);
        }

        UpdateFares(flight, request.Fares);

        flight.FlightNumber = flightNumber;
        flight.FlightScheduleId = request.FlightScheduleId;
        flight.RouteId = route.Id;
        flight.AircraftId = aircraft.Id;
        flight.DepartureAt = departureAt;
        flight.ArrivalAt = arrivalAt;
        flight.Status = request.Status;
        _flightRepository.Update(flight);

        if (hasBookings && (statusChanged || timesChanged))
        {
            await NotifyPassengersAsync(flight, statusChanged, timesChanged, cancellationToken);
        }

        await SaveAsync(cancellationToken);
        return await GetResultAsync(flight.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var flight = await _flightRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Flight not found.");

        if (await _flightRepository.HasBookingsAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("This flight has bookings. Set its status to Cancelled instead of deleting it.");
        }

        _flightRepository.Delete(flight);
        await _flightRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<SeatInventoryDto> GetSeatInventoryAsync(int flightId, CabinClass? cabinClass = null, CancellationToken cancellationToken = default)
    {
        var flight = await _flightRepository.GetByIdAsync(flightId, cancellationToken)
                     ?? throw new NotFoundException("Flight not found.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var seats = (await _flightSeatRepository.GetByFlightIdAsync(flightId, cancellationToken))
            .Where(s => cabinClass is null || s.AircraftSeat.CabinClass == cabinClass)
            .Select(s => ToInventorySeat(s, now))
            .ToList();

        int Count(SeatStatus status) => seats.Count(s => s.Status == status);

        return new SeatInventoryDto(flight.Id, flight.FlightNumber,
            Count(SeatStatus.Available), Count(SeatStatus.Held), Count(SeatStatus.Booked), Count(SeatStatus.Blocked), seats);
    }

    public async Task<InventorySeatDto> SetSeatBlockedAsync(int flightId, int flightSeatId, bool blocked, CancellationToken cancellationToken = default)
    {
        var seat = (await _flightSeatRepository.GetTrackedByIdsAsync(flightId, new[] { flightSeatId }, cancellationToken)).SingleOrDefault()
                   ?? throw new NotFoundException("Seat not found on this flight.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var label = SeatService.Label(seat.AircraftSeat);
        if (blocked)
        {
            if (!SeatService.IsFree(seat, now))
            {
                throw new BusinessRuleException($"Seat {label} is reserved or occupied and cannot be blocked.");
            }

            seat.Status = SeatStatus.Blocked;
            seat.HeldUntil = null;
        }
        else
        {
            if (seat.Status != SeatStatus.Blocked)
            {
                throw new BusinessRuleException($"Seat {label} is not blocked.");
            }

            seat.Status = SeatStatus.Available;
        }

        _flightSeatRepository.Update(seat);
        try
        {
            await _flightSeatRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException($"Seat {label} was just changed by a booking. Refresh the seat map and try again.");
        }

        return ToInventorySeat(seat, now);
    }

    private static InventorySeatDto ToInventorySeat(FlightSeat seat, DateTime now)
        => new(seat.Id, seat.AircraftSeat.RowNumber, seat.AircraftSeat.Letter, SeatService.Label(seat.AircraftSeat), seat.AircraftSeat.CabinClass,
            seat.Status == SeatStatus.Held && SeatService.IsFree(seat, now) ? SeatStatus.Available : seat.Status);

    // Inputs without a time zone are treated as UTC, like every stored time.
    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static FlightSegment DirectSegment(Route route, DateTime departureAt, DateTime arrivalAt)
        => new()
        {
            SequenceNo = 1,
            OriginAirportId = route.OriginAirportId,
            DestinationAirportId = route.DestinationAirportId,
            DepartureAt = departureAt,
            ArrivalAt = arrivalAt
        };

    /// <summary>Keeps the stops: every segment moves with the new departure, and the last one lands at the new arrival time.</summary>
    private static void ShiftSegments(Flight flight, DateTime departureAt, DateTime arrivalAt)
    {
        var shift = departureAt - flight.DepartureAt;
        var segments = flight.Segments.OrderBy(s => s.SequenceNo).ToList();
        foreach (var segment in segments)
        {
            segment.DepartureAt += shift;
            segment.ArrivalAt += shift;
        }

        segments[^1].ArrivalAt = arrivalAt;
        if (segments[^1].ArrivalAt <= segments[^1].DepartureAt)
        {
            throw new BusinessRuleException("The new arrival time is before the last stop's departure.");
        }
    }

    private static void AddSeatInventory(Flight flight, Aircraft aircraft)
    {
        foreach (var seat in aircraft.Seats)
        {
            flight.Seats.Add(new FlightSeat { AircraftSeatId = seat.Id, Status = SeatStatus.Available });
        }
    }

    private static void UpdateFares(Flight flight, IReadOnlyDictionary<CabinClass, decimal> fares)
    {
        foreach (var removed in flight.Fares.Where(f => !fares.ContainsKey(f.CabinClass)).ToList())
        {
            flight.Fares.Remove(removed);
        }

        foreach (var (cabin, price) in fares)
        {
            var fare = flight.Fares.FirstOrDefault(f => f.CabinClass == cabin);
            if (fare is null)
            {
                flight.Fares.Add(new FlightFare { CabinClass = cabin, BasePrice = price });
            }
            else
            {
                fare.BasePrice = price;
            }
        }
    }

    internal static void CheckFaresMatchAircraft(IEnumerable<CabinClass> cabins, Aircraft aircraft)
    {
        var missing = cabins.Where(cabin => aircraft.Seats.All(s => s.CabinClass != cabin)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"Aircraft {aircraft.Name} has no {string.Join(", ", missing)} seats.");
        }
    }

    private async Task NotifyPassengersAsync(Flight flight, bool statusChanged, bool timesChanged, CancellationToken cancellationToken)
    {
        var message = statusChanged && flight.Status == FlightStatus.Cancelled
            ? $"Flight {flight.FlightNumber} on {flight.DepartureAt:dd MMM yyyy} has been cancelled."
            : timesChanged
                ? $"Flight {flight.FlightNumber} now departs at {flight.DepartureAt:dd MMM yyyy HH:mm} UTC and arrives at {flight.ArrivalAt:HH:mm} UTC."
                : $"The status of flight {flight.FlightNumber} on {flight.DepartureAt:dd MMM yyyy} is now {flight.Status}.";

        foreach (var userId in await _bookingRepository.GetUserIdsWithActiveBookingsOnFlightAsync(flight.Id, cancellationToken))
        {
            await _notificationService.QueueAsync(userId, null, NotificationType.FlightUpdate, "Flight Update", message, cancellationToken);
        }
    }

    private void EnsureDepartureInFuture(DateTime departureAt)
    {
        if (departureAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(FlightRequestDto.DepartureAt), "The departure must be in the future.")
            });
        }
    }

    private async Task EnsureUniqueAsync(string flightNumber, DateTime departureAt, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _flightRepository.ExistsAsync(flightNumber, departureAt, excludeId, cancellationToken))
        {
            throw new ConflictException($"Flight {flightNumber} already departs at {departureAt:dd MMM yyyy HH:mm}.");
        }
    }

    private async Task<Route> GetActiveRouteAsync(FlightRequestDto request, CancellationToken cancellationToken)
    {
        var origin = await _airportRepository.GetByCodeAsync(request.OriginCode.Trim().ToUpperInvariant(), cancellationToken)
                     ?? throw new NotFoundException($"Airport '{request.OriginCode}' was not found.");
        var destination = await _airportRepository.GetByCodeAsync(request.DestinationCode.Trim().ToUpperInvariant(), cancellationToken)
                          ?? throw new NotFoundException($"Airport '{request.DestinationCode}' was not found.");

        var route = await _routeRepository.GetByAirportsAsync(origin.Id, destination.Id, cancellationToken)
                    ?? throw new BusinessRuleException($"There is no route {origin.Code}-{destination.Code}. Create the route first.");

        return route.IsActive ? route : throw new BusinessRuleException($"The route {origin.Code}-{destination.Code} is inactive.");
    }

    private async Task<Aircraft> GetUsableAircraftAsync(int aircraftId, CancellationToken cancellationToken)
    {
        var aircraft = await _aircraftRepository.GetWithSeatsAsync(aircraftId, cancellationToken)
                       ?? throw new NotFoundException("Aircraft not found.");

        return aircraft.Status == AircraftStatus.Active
            ? aircraft
            : throw new BusinessRuleException($"Aircraft {aircraft.Name} is {aircraft.Status} and cannot be assigned to a flight.");
    }

    private async Task CheckScheduleAsync(int? scheduleId, CancellationToken cancellationToken)
    {
        if (scheduleId is int id && await _scheduleRepository.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Schedule not found.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _flightRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("The flight could not be saved because it conflicts with existing data. Refresh and try again.");
        }
    }

    private async Task<FlightResultDto> GetResultAsync(int id, CancellationToken cancellationToken)
        => (await _flightRepository.GetDetailsAsync(id, cancellationToken))?.ToResultDto()
           ?? throw new NotFoundException("Flight not found.");
}
