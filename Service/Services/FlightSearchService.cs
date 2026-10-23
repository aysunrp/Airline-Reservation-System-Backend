using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Scheduling;
using Service.Services.Interfaces;

namespace Service.Services;

public class FlightSearchService : IFlightSearchService
{
    private readonly IAirportRepository _airportRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IValidator<FlightSearchRequestDto> _validator;
    private readonly IValidator<LowestFaresRequestDto> _lowestFaresValidator;
    private readonly TimeProvider _timeProvider;

    public FlightSearchService(
        IAirportRepository airportRepository,
        IFlightRepository flightRepository,
        IFlightSeatRepository flightSeatRepository,
        IValidator<FlightSearchRequestDto> validator,
        IValidator<LowestFaresRequestDto> lowestFaresValidator,
        TimeProvider timeProvider)
    {
        _airportRepository = airportRepository;
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _validator = validator;
        _lowestFaresValidator = lowestFaresValidator;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<FlightSearchLegResultDto>> SearchAsync(FlightSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var results = new List<FlightSearchLegResultDto>(request.Legs.Count);
        for (var i = 0; i < request.Legs.Count; i++)
        {
            var leg = request.Legs[i];
            var origin = await GetAirportAsync(leg.OriginCode, cancellationToken);
            var destination = await GetAirportAsync(leg.DestinationCode, cancellationToken);

            // The date is the departure day at the origin airport, from its local midnight to the next one.
            var fromUtc = ScheduleTimeCalculator.ToUtc(leg.Date, TimeOnly.MinValue, origin);
            var toUtc = ScheduleTimeCalculator.ToUtc(leg.Date.AddDays(1), TimeOnly.MinValue, origin);
            var flights = await _flightRepository.SearchAsync(origin.Id, destination.Id, fromUtc, toUtc, cancellationToken);
            var bookable = await FilterBookableAsync(flights, request.CabinClass, request.Passengers, cancellationToken);
            results.Add(new FlightSearchLegResultDto(i, origin.Code, destination.Code, leg.Date, bookable.Select(f => f.ToResultDto()).ToList()));
        }

        return results;
    }

    public async Task<IReadOnlyList<DayFareDto>> GetLowestFaresAsync(LowestFaresRequestDto request, CancellationToken cancellationToken = default)
    {
        await _lowestFaresValidator.ValidateAndThrowAsync(request, cancellationToken);

        var origin = await GetAirportAsync(request.OriginCode, cancellationToken);
        var destination = await GetAirportAsync(request.DestinationCode, cancellationToken);

        // The same rules as a search: days are departure days at the origin airport, and a flight counts only if it can still be booked.
        var fromUtc = ScheduleTimeCalculator.ToUtc(request.From, TimeOnly.MinValue, origin);
        var toUtc = ScheduleTimeCalculator.ToUtc(request.To.AddDays(1), TimeOnly.MinValue, origin);
        var flights = await _flightRepository.SearchAsync(origin.Id, destination.Id, fromUtc, toUtc, cancellationToken);
        var bookable = await FilterBookableAsync(flights, request.CabinClass, request.Passengers, cancellationToken);

        return bookable
            .Select(f => new
            {
                Date = ScheduleTimeCalculator.LocalDate(f.DepartureAt, origin),
                Fare = f.Fares.Where(fare => request.CabinClass is null || fare.CabinClass == request.CabinClass).Min(fare => fare.BasePrice)
            })
            .GroupBy(x => x.Date)
            .Select(g => new DayFareDto(g.Key, g.Min(x => x.Fare)))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<FlightResultDto> GetFlightAsync(int flightId, CancellationToken cancellationToken = default)
    {
        var flight = await _flightRepository.GetDetailsAsync(flightId, cancellationToken)
                     ?? throw new NotFoundException("Flight not found.");
        return flight.ToResultDto();
    }

    /// <summary>Keeps flights that sell the requested cabin and still have a free seat for every traveller.</summary>
    private async Task<IReadOnlyList<Domain.Entities.Flight>> FilterBookableAsync(
        IReadOnlyList<Domain.Entities.Flight> flights, Domain.Enums.CabinClass? cabinClass, int passengers, CancellationToken cancellationToken)
    {
        var candidates = flights
            .Where(f => cabinClass is null || f.Fares.Any(fare => fare.CabinClass == cabinClass))
            .ToList();
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var freeSeats = await _flightSeatRepository.GetFreeSeatCountsAsync(
            candidates.Select(f => f.Id).ToList(), cabinClass, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        return candidates
            .Where(f => freeSeats.TryGetValue(f.Id, out var free) && free >= passengers)
            .ToList();
    }

    private async Task<Domain.Entities.Airport> GetAirportAsync(string code, CancellationToken cancellationToken)
    {
        var airport = await _airportRepository.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken);
        return airport is { IsActive: true }
            ? airport
            : throw new NotFoundException($"Airport '{code}' was not found.");
    }
}
