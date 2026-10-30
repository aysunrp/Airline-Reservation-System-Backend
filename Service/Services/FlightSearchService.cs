using FluentValidation;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Scheduling;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class FlightSearchService : IFlightSearchService
{
    private readonly IAirportRepository _airportRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IValidator<FlightSearchRequestDto> _validator;
    private readonly IValidator<LowestFaresRequestDto> _lowestFaresValidator;
    private readonly IValidator<FlightRangeSearchRequestDto> _rangeValidator;
    private readonly VoiceSearchSettings _voiceSettings;
    private readonly TimeProvider _timeProvider;

    public FlightSearchService(
        IAirportRepository airportRepository,
        IFlightRepository flightRepository,
        IFlightSeatRepository flightSeatRepository,
        IValidator<FlightSearchRequestDto> validator,
        IValidator<LowestFaresRequestDto> lowestFaresValidator,
        IValidator<FlightRangeSearchRequestDto> rangeValidator,
        IOptions<VoiceSearchSettings> voiceSettings,
        TimeProvider timeProvider)
    {
        _airportRepository = airportRepository;
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _validator = validator;
        _lowestFaresValidator = lowestFaresValidator;
        _rangeValidator = rangeValidator;
        _voiceSettings = voiceSettings.Value;
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

    public async Task<FlightRangeSearchResultDto> SearchRangeAsync(FlightRangeSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        await _rangeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var origin = await GetAirportAsync(request.OriginCode, cancellationToken);
        var destination = await GetAirportAsync(request.DestinationCode, cancellationToken);

        // The same rules as a search: days are departure days at the origin airport, and a flight counts only if it can still be booked.
        var fromUtc = ScheduleTimeCalculator.ToUtc(request.From, TimeOnly.MinValue, origin);
        var toUtc = ScheduleTimeCalculator.ToUtc(request.To.AddDays(1), TimeOnly.MinValue, origin);
        var flights = await _flightRepository.SearchAsync(origin.Id, destination.Id, fromUtc, toUtc, cancellationToken);
        var bookable = await FilterBookableAsync(flights, request.CabinClass, request.Passengers, cancellationToken);

        var matching = bookable
            .Where(f => request.TimeOfDay is null
                        || DepartureTimeOfDayRanges.Contains(request.TimeOfDay.Value, ScheduleTimeCalculator.LocalTime(f.DepartureAt, origin)))
            .Select(f => new { Flight = f, Price = f.Fares.Where(fare => request.CabinClass is null || fare.CabinClass == request.CabinClass).Min(fare => fare.BasePrice) });

        var ordered = request.SortBy == "departure"
            ? matching.OrderBy(x => x.Flight.DepartureAt).ThenBy(x => x.Price).ThenBy(x => x.Flight.Id)
            : matching.OrderBy(x => x.Price).ThenBy(x => x.Flight.DepartureAt).ThenBy(x => x.Flight.Id);
        var all = ordered.ToList();

        // Every flight of the whole period is compared first. The lowest price is the minimum of all of them, and a lowest-only search keeps
        // every flight at that price (never just the first one) and no dearer flight. Fares are all in one currency.
        var lowest = all.Count == 0 ? 0 : all.Min(x => x.Price);
        var lowestCount = all.Count(x => x.Price == lowest);
        var chosen = request.LowestOnly ? all.Where(x => x.Price == lowest) : all;
        var limit = Math.Max(1, _voiceSettings.MaxResults);
        return new FlightRangeSearchResultDto(origin.Code, destination.Code, request.From, request.To, request.SortBy, request.TimeOfDay,
            all.Count, lowestCount, Domain.Constants.AppConstants.Currency, chosen.Take(limit).Select(x => x.Flight.ToResultDto()).ToList());
    }

    public async Task<FlightResultDto> GetFlightAsync(int flightId, CancellationToken cancellationToken = default)
    {
        var flight = await _flightRepository.GetDetailsAsync(flightId, cancellationToken)
                     ?? throw new NotFoundException("Uçuş tapılmadı.");
        return flight.ToResultDto();
    }

    /// <summary>Keeps flights that have not departed, sell the requested cabin and still have a free seat for every traveller.</summary>
    private async Task<IReadOnlyList<Domain.Entities.Flight>> FilterBookableAsync(
        IReadOnlyList<Domain.Entities.Flight> flights, Domain.Enums.CabinClass? cabinClass, int passengers, CancellationToken cancellationToken)
    {
        // The same rule booking applies, so a flight that is listed here can still be booked.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var candidates = flights
            .Where(f => f.Status != Domain.Enums.FlightStatus.Completed && f.DepartureAt > now)
            .Where(f => cabinClass is null || f.Fares.Any(fare => fare.CabinClass == cabinClass))
            .ToList();
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var freeSeats = await _flightSeatRepository.GetFreeSeatCountsAsync(
            candidates.Select(f => f.Id).ToList(), cabinClass, now, cancellationToken);

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
