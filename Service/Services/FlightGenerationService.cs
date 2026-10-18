using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Scheduling;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Creates dated flights from active schedules: UTC times from the airports' time zones, one segment per leg,
/// the schedule's cabin fares and a seat inventory from the aircraft's layout. Flights that already exist are left alone.
/// The checks before inserting keep repeated runs cheap; the unique indexes on (flight number, departure) and
/// (schedule, scheduled date) are what make concurrent runs safe.
/// </summary>
public class FlightGenerationService : IFlightGenerationService
{
    private const int MaxDays = 366;
    private const int FlightsPerBatch = 25;

    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly FlightGenerationSettings _settings;
    private readonly TimeProvider _timeProvider;

    public FlightGenerationService(
        IFlightScheduleRepository scheduleRepository,
        IFlightRepository flightRepository,
        IAircraftRepository aircraftRepository,
        IOptions<FlightGenerationSettings> settings,
        TimeProvider timeProvider)
    {
        _scheduleRepository = scheduleRepository;
        _flightRepository = flightRepository;
        _aircraftRepository = aircraftRepository;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<FlightGenerationResultDto> GenerateAsync(int? scheduleId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        CheckRange(from, to);

        var schedules = scheduleId is int id
            ? new[] { await _scheduleRepository.GetWithDetailsAsync(id, cancellationToken) ?? throw new NotFoundException("Schedule not found.") }
            : (await _scheduleRepository.GetAllWithDetailsAsync(cancellationToken)).Where(s => s.IsActive).ToArray();

        var run = new Run(_timeProvider.GetUtcNow().UtcDateTime);
        foreach (var schedule in schedules)
        {
            await GenerateForScheduleAsync(schedule, from, to, run, cancellationToken);
            _flightRepository.ClearTracking();
        }

        return new FlightGenerationResultDto(run.Created, run.AlreadyExisting, run.Skipped, run.Conflicts, run.Adjustments);
    }

    private async Task GenerateForScheduleAsync(FlightSchedule schedule, DateOnly? from, DateOnly? to, Run run, CancellationToken cancellationToken)
    {
        void Skip(string reason, DateOnly? date = null)
            => run.Skipped.Add(new FlightGenerationSkipDto(schedule.Id, schedule.FlightNumber, date, reason));

        var aircraft = await GetUsableAircraftAsync(schedule, Skip, cancellationToken);
        if (aircraft is null)
        {
            return;
        }

        DateOnly firstDate;
        try
        {
            firstDate = from ?? ScheduleTimeCalculator.LocalDate(run.Now, schedule.Route.Origin);
        }
        catch (BusinessRuleException ex)
        {
            Skip(ex.Message);
            return;
        }

        var lastDate = to ?? firstDate.AddDays(Math.Max(1, _settings.DaysAhead) - 1);

        // A local day can start up to 14 hours before or after midnight UTC; a day of margin covers every zone.
        var existing = (await _flightRepository.GetDepartureKeysAsync(
                firstDate.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lastDate.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                cancellationToken))
            .ToHashSet();
        var generatedDays = (await _flightRepository.GetScheduledDatesAsync(schedule.Id, firstDate, lastDate, cancellationToken)).ToHashSet();

        var savedInBatch = 0;
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            if (!AdminScheduleService.OperatesOn(schedule, date))
            {
                continue;
            }

            ScheduledFlightTimes times;
            try
            {
                times = ScheduleTimeCalculator.Calculate(schedule, date);
            }
            catch (BusinessRuleException ex)
            {
                Skip(ex.Message, date);
                continue;
            }

            if (times.DepartureAt <= run.Now)
            {
                Skip("The departure time has already passed.", date);
                continue;
            }

            // The day already has its flight (even if the schedule's times changed since), or another flight
            // uses this number at this time.
            if (generatedDays.Contains(date) || !existing.Add((schedule.FlightNumber, times.DepartureAt)))
            {
                run.AlreadyExisting++;
                continue;
            }

            // Each flight is saved on its own together with its segments, fares and seats, so it is never stored
            // half-built, and losing a race for one day does not stop the others.
            var flight = BuildFlight(schedule, aircraft, times);
            if (!await _flightRepository.TryAddAsync(flight, cancellationToken))
            {
                run.Conflicts.Add(new FlightGenerationConflictDto(schedule.Id, schedule.FlightNumber, date, times.DepartureAt));
                continue;
            }

            run.Created.Add(new GeneratedFlightDto(flight.Id, schedule.Id, flight.FlightNumber, date, flight.DepartureAt, flight.ArrivalAt));
            run.Adjustments.AddRange(times.Adjustments.Select(a => $"{schedule.FlightNumber}: {a}"));

            if (++savedInBatch == FlightsPerBatch)
            {
                _flightRepository.ClearTracking();
                savedInBatch = 0;
            }
        }
    }

    private async Task<Aircraft?> GetUsableAircraftAsync(FlightSchedule schedule, Action<string, DateOnly?> skip, CancellationToken cancellationToken)
    {
        if (!schedule.IsActive)
        {
            skip("The schedule is inactive.", null);
            return null;
        }

        if (!schedule.Route.IsActive)
        {
            skip($"The route {schedule.Route.Origin.Code}-{schedule.Route.Destination.Code} is inactive.", null);
            return null;
        }

        if (schedule.Fares.Count == 0)
        {
            skip("The schedule has no cabin fares.", null);
            return null;
        }

        if (schedule.DefaultAircraftId is not int aircraftId)
        {
            skip("The schedule has no aircraft assigned.", null);
            return null;
        }

        var aircraft = await _aircraftRepository.GetWithSeatsAsync(aircraftId, cancellationToken);
        if (aircraft is null || aircraft.Status != AircraftStatus.Active)
        {
            skip(aircraft is null ? "The assigned aircraft no longer exists." : $"Aircraft {aircraft.Name} is {aircraft.Status}.", null);
            return null;
        }

        try
        {
            AdminFlightService.CheckFaresMatchAircraft(schedule.Fares.Select(f => f.CabinClass), aircraft);
        }
        catch (BusinessRuleException ex)
        {
            skip(ex.Message, null);
            return null;
        }

        return aircraft;
    }

    // Only foreign keys are set: the schedule, route and aircraft may come from an untracked query.
    private static Flight BuildFlight(FlightSchedule schedule, Aircraft aircraft, ScheduledFlightTimes times)
    {
        var flight = new Flight
        {
            FlightNumber = schedule.FlightNumber,
            FlightScheduleId = schedule.Id,
            ScheduledDate = times.ScheduledDate,
            RouteId = schedule.RouteId,
            AircraftId = aircraft.Id,
            DepartureAt = times.DepartureAt,
            ArrivalAt = times.ArrivalAt,
            Status = FlightStatus.Scheduled
        };

        foreach (var segment in times.Segments)
        {
            flight.Segments.Add(new FlightSegment
            {
                SequenceNo = segment.SequenceNo,
                OriginAirportId = segment.OriginAirportId,
                DestinationAirportId = segment.DestinationAirportId,
                DepartureAt = segment.DepartureAt,
                ArrivalAt = segment.ArrivalAt,
                LayoverMinutes = segment.LayoverMinutes
            });
        }

        foreach (var fare in schedule.Fares)
        {
            flight.Fares.Add(new FlightFare { CabinClass = fare.CabinClass, BasePrice = fare.BasePrice });
        }

        AdminFlightService.AddSeatInventory(flight, aircraft);
        return flight;
    }

    private static void CheckRange(DateOnly? from, DateOnly? to)
    {
        var errors = new List<ValidationFailure>();
        if (from is not null && to is not null)
        {
            if (to < from)
            {
                errors.Add(new ValidationFailure("To", "The end date cannot be before the start date."));
            }
            else if (to.Value.DayNumber - from.Value.DayNumber >= MaxDays)
            {
                errors.Add(new ValidationFailure("To", $"Flights can be generated for at most {MaxDays} days at a time."));
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private sealed class Run
    {
        public Run(DateTime now) => Now = now;

        public DateTime Now { get; }
        public List<GeneratedFlightDto> Created { get; } = new();
        public int AlreadyExisting { get; set; }
        public List<FlightGenerationSkipDto> Skipped { get; } = new();
        public List<FlightGenerationConflictDto> Conflicts { get; } = new();
        public List<string> Adjustments { get; } = new();
    }
}
