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
    private readonly IValidator<ScheduleRequestDto> _validator;

    public AdminScheduleService(
        IFlightScheduleRepository scheduleRepository,
        IRouteRepository routeRepository,
        IAircraftRepository aircraftRepository,
        IValidator<ScheduleRequestDto> validator)
    {
        _scheduleRepository = scheduleRepository;
        _routeRepository = routeRepository;
        _aircraftRepository = aircraftRepository;
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
    }

    private async Task<FlightSchedule> GetScheduleAsync(int id, CancellationToken cancellationToken)
        => await _scheduleRepository.GetWithDetailsAsync(id, cancellationToken)
           ?? throw new NotFoundException("Schedule not found.");
}
