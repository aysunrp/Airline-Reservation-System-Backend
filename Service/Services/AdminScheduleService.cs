using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Validators;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminScheduleService : IAdminScheduleService
{
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly IAirportRepository _airportRepository;
    private readonly IValidator<ScheduleRequestDto> _validator;

    public AdminScheduleService(
        IFlightScheduleRepository scheduleRepository,
        IRouteRepository routeRepository,
        IAircraftRepository aircraftRepository,
        IAirportRepository airportRepository,
        IValidator<ScheduleRequestDto> validator)
    {
        _scheduleRepository = scheduleRepository;
        _routeRepository = routeRepository;
        _aircraftRepository = aircraftRepository;
        _airportRepository = airportRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetAllAsync(DateOnly? operatingOn = null, CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.GetAllWithDetailsAsync(cancellationToken);
        return schedules
            .Where(s => operatingOn is null || OperatesOn(s, operatingOn.Value))
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<ScheduleDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => (await GetScheduleAsync(id, cancellationToken)).ToDto();

    public async Task<ScheduleDto> CreateAsync(ScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var schedule = new FlightSchedule();
        await ApplyAsync(request, schedule, cancellationToken);

        await _scheduleRepository.AddAsync(schedule, cancellationToken);
        await _scheduleRepository.SaveChangesAsync(cancellationToken);

        return schedule.ToDto();
    }

    public async Task<ScheduleDto> UpdateAsync(int id, ScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var schedule = await GetScheduleAsync(id, cancellationToken);

        await ApplyAsync(request, schedule, cancellationToken);
        _scheduleRepository.Update(schedule);
        await _scheduleRepository.SaveChangesAsync(cancellationToken);

        return schedule.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleAsync(id, cancellationToken);
        _scheduleRepository.Delete(schedule);
        await _scheduleRepository.SaveChangesAsync(cancellationToken);
    }

    internal static bool OperatesOn(FlightSchedule schedule, DateOnly date)
        => schedule.IsActive
           && schedule.ValidFrom <= date
           && (schedule.ValidTo is null || date <= schedule.ValidTo)
           && schedule.DaysOfWeek.HasFlag(ToScheduleDay(date.DayOfWeek));

    private static ScheduleDays ToScheduleDay(DayOfWeek day)
        => day == DayOfWeek.Sunday ? ScheduleDays.Sunday : (ScheduleDays)(1 << ((int)day - 1));

    private async Task ApplyAsync(ScheduleRequestDto request, FlightSchedule schedule, CancellationToken cancellationToken)
    {
        var route = await _routeRepository.GetWithAirportsAsync(request.RouteId, cancellationToken)
                    ?? throw new NotFoundException("Route not found.");
        if (!route.IsActive)
        {
            throw new BusinessRuleException("Schedules can only be created on active routes.");
        }

        Aircraft? aircraft = null;
        if (request.DefaultAircraftId is int aircraftId)
        {
            aircraft = await _aircraftRepository.GetByIdAsync(aircraftId, cancellationToken)
                       ?? throw new NotFoundException("Aircraft not found.");
            if (aircraft.Status == AircraftStatus.Inactive)
            {
                throw new BusinessRuleException($"Aircraft {aircraft.Name} is inactive.");
            }
        }

        schedule.FlightNumber = FlightNumber.Normalize(request.FlightNumber);
        schedule.RouteId = route.Id;
        schedule.Route = route;
        schedule.DefaultAircraftId = aircraft?.Id;
        schedule.DefaultAircraft = aircraft;
        schedule.DepartureTime = request.DepartureTime;
        schedule.ArrivalTime = request.ArrivalTime;
        schedule.ArrivalDayOffset = request.ArrivalDayOffset;
        schedule.DaysOfWeek = request.DaysOfWeek;
        schedule.ValidFrom = request.ValidFrom;
        schedule.ValidTo = request.ValidTo;
        schedule.IsActive = request.IsActive;

        ApplyFares(request.Fares, schedule);
        await ApplyStopsAsync(request.Stops, route, schedule, cancellationToken);
    }

    // Existing rows are updated in place so the unique (schedule, cabin) and (schedule, stop number)
    // indexes never see a removed row and its replacement at the same time.
    private static void ApplyFares(List<ScheduleFareRequestDto> fares, FlightSchedule schedule)
    {
        foreach (var removed in schedule.Fares.Where(f => fares.All(r => r.CabinClass != f.CabinClass)).ToList())
        {
            schedule.Fares.Remove(removed);
        }

        foreach (var fare in fares)
        {
            var existing = schedule.Fares.FirstOrDefault(f => f.CabinClass == fare.CabinClass);
            if (existing is null)
            {
                schedule.Fares.Add(new FlightScheduleFare { CabinClass = fare.CabinClass, BasePrice = fare.BasePrice });
            }
            else
            {
                existing.BasePrice = fare.BasePrice;
            }
        }
    }

    private async Task ApplyStopsAsync(List<ScheduleStopRequestDto> stops, Route route, FlightSchedule schedule, CancellationToken cancellationToken)
    {
        var airports = new Dictionary<int, Airport>();
        foreach (var stop in stops)
        {
            var code = stop.AirportCode.Trim().ToUpperInvariant();
            var airport = await _airportRepository.GetByCodeAsync(code, cancellationToken)
                          ?? throw new NotFoundException($"Airport '{code}' was not found.");
            if (airport.Id == route.OriginAirportId || airport.Id == route.DestinationAirportId)
            {
                throw new BusinessRuleException($"{airport.Code} is the origin or destination of the route, so it cannot be a stop.");
            }

            airports[stop.SequenceNo] = airport;
        }

        foreach (var removed in schedule.Stops.Where(s => stops.All(r => r.SequenceNo != s.SequenceNo)).ToList())
        {
            schedule.Stops.Remove(removed);
        }

        foreach (var stop in stops.OrderBy(s => s.SequenceNo))
        {
            var existing = schedule.Stops.FirstOrDefault(s => s.SequenceNo == stop.SequenceNo);
            if (existing is null)
            {
                existing = new FlightScheduleStop { SequenceNo = stop.SequenceNo };
                schedule.Stops.Add(existing);
            }

            var airport = airports[stop.SequenceNo];
            existing.AirportId = airport.Id;
            existing.Airport = airport;
            existing.ArrivalOffsetMinutes = stop.ArrivalOffsetMinutes;
            existing.LayoverMinutes = stop.LayoverMinutes;
        }
    }

    private async Task<FlightSchedule> GetScheduleAsync(int id, CancellationToken cancellationToken)
        => await _scheduleRepository.GetWithDetailsAsync(id, cancellationToken)
           ?? throw new NotFoundException("Schedule not found.");
}
