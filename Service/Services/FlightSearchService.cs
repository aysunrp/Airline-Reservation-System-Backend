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
    private readonly IRouteRepository? _routeRepository;

    public FlightSearchService(
        IAirportRepository airportRepository,
        IFlightRepository flightRepository,
        IFlightSeatRepository flightSeatRepository,
        IValidator<FlightSearchRequestDto> validator,
        IValidator<LowestFaresRequestDto> lowestFaresValidator,
        IValidator<FlightRangeSearchRequestDto> rangeValidator,
        IOptions<VoiceSearchSettings> voiceSettings,
        TimeProvider timeProvider,
        IRouteRepository? routeRepository = null)
    {
        _routeRepository = routeRepository;
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
            var connections = await FindConnectionsAsync(origin, destination, fromUtc, toUtc, request.CabinClass, request.Passengers, cancellationToken);
            results.Add(new FlightSearchLegResultDto(i, origin.Code, destination.Code, leg.Date, bookable.Select(f => f.ToResultDto()).ToList()) { Connections = connections });
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

        // A day with a connection but no direct flight is a day with a journey, and its price is the sum of its flights.
        var connections = await FindConnectionsAsync(origin, destination, fromUtc, toUtc, request.CabinClass, request.Passengers, cancellationToken);

        return bookable
            .Select(f => new
            {
                Date = ScheduleTimeCalculator.LocalDate(f.DepartureAt, origin),
                Fare = f.Fares.Where(fare => request.CabinClass is null || fare.CabinClass == request.CabinClass).Min(fare => fare.BasePrice)
            })
            .Concat(connections.Select(c => new
            {
                Date = ScheduleTimeCalculator.LocalDate(c.DepartureAt, origin),
                Fare = c.Fares.Where(fare => request.CabinClass is null || fare.Key == request.CabinClass).Min(fare => fare.Value)
            }))
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
            // A direct flight has one segment; a flight with a stop has more (the stop is where one segment ends and the next begins).
            .Where(f => request.Stops is null || (request.Stops == "direct") == (f.Segments.Count <= 1))
            .Select(f => new RangeCandidate(
                f, null, f.Fares.Where(fare => request.CabinClass is null || fare.CabinClass == request.CabinClass).Min(fare => fare.BasePrice),
                f.DepartureAt, f.ArrivalAt - f.DepartureAt, f.Id))
            .ToList();

        // Journeys of two flights through a transfer airport are part of the period too (never for "direct only"), found by the same rules a
        // one-day search and booking apply. Every one of the period is kept here: the cap of a one-day search would drop most of 62 days.
        if (request.Stops != "direct")
        {
            var journeys = await FindConnectionsAsync(origin, destination, fromUtc, toUtc, request.CabinClass, request.Passengers, cancellationToken, int.MaxValue);
            matching.AddRange(journeys
                .Where(c => request.TimeOfDay is null
                            || DepartureTimeOfDayRanges.Contains(request.TimeOfDay.Value, ScheduleTimeCalculator.LocalTime(c.DepartureAt, origin)))
                .Select(c => new RangeCandidate(
                    null, c, c.Fares.Where(fare => request.CabinClass is null || fare.Key == request.CabinClass).Min(fare => fare.Value),
                    c.DepartureAt, TimeSpan.FromMinutes(c.DurationMinutes), c.Flights[0].Id)));
        }

        // Every matching flight and journey of the whole period is ordered before the list is cut, so "shortest" is the shortest of all of them.
        var ordered = request.SortBy switch
        {
            "departure" => matching.OrderBy(x => x.DepartureAt).ThenBy(x => x.Price).ThenBy(x => x.Id),
            "duration" => matching.OrderBy(x => x.Duration).ThenBy(x => x.Price).ThenBy(x => x.DepartureAt).ThenBy(x => x.Id),
            _ => matching.OrderBy(x => x.Price).ThenBy(x => x.DepartureAt).ThenBy(x => x.Id)
        };
        var all = ordered.ToList();

        // Every flight of the whole period is compared first. The lowest price is the minimum of all of them, and a lowest-only search keeps
        // every flight at that price (never just the first one) and no dearer flight. Fares are all in one currency.
        var lowest = all.Count == 0 ? 0 : all.Min(x => x.Price);
        var lowestCount = all.Count(x => x.Price == lowest);
        var chosen = request.LowestOnly ? all.Where(x => x.Price == lowest) : all;
        var limit = Math.Max(1, _voiceSettings.MaxResults);
        var shown = chosen.Take(limit).ToList();
        return new FlightRangeSearchResultDto(origin.Code, destination.Code, request.From, request.To, request.SortBy, request.TimeOfDay,
            all.Count, lowestCount, Domain.Constants.AppConstants.Currency,
            shown.Where(x => x.Flight is not null).Select(x => x.Flight!.ToResultDto()).ToList(), request.Stops)
        {
            Connections = shown.Where(x => x.Connection is not null).Select(x => x.Connection!).ToList()
        };
    }

    /// <summary>A flight, or a journey of two flights, of a period search with what it is ordered by.</summary>
    private sealed record RangeCandidate(Domain.Entities.Flight? Flight, ConnectionResultDto? Connection, decimal Price, DateTime DepartureAt, TimeSpan Duration, int Id);

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
            // A flight that stops at, or starts from, an airport that is switched off is not sold any more.
            .Where(f => f.Segments.All(s => s.Origin.IsActive && s.Destination.IsActive))
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

    /// <summary>
    /// Journeys of two flights through one transfer airport: a hub that an active route joins to both ends, a first flight that departs in
    /// the window and a second one that departs 90 minutes to 8 hours after the first lands. Both flights must sell the cabin and have free
    /// seats for everyone, exactly as booking will require. Nothing is stored: a connection exists only while both of its flights do.
    /// </summary>
    private async Task<IReadOnlyList<ConnectionResultDto>> FindConnectionsAsync(
        Domain.Entities.Airport origin, Domain.Entities.Airport destination, DateTime fromUtc, DateTime toUtc,
        Domain.Enums.CabinClass? cabinClass, int passengers, CancellationToken cancellationToken, int? limit = null)
    {
        if (_routeRepository is null)
        {
            return Array.Empty<ConnectionResultDto>();
        }

        var pairs = await _routeRepository.GetActivePairsAsync(cancellationToken);
        var fromOrigin = pairs.Where(p => p.Origin == origin.Id).Select(p => p.Destination).ToHashSet();
        var hubs = pairs.Where(p => p.Destination == destination.Id && fromOrigin.Contains(p.Origin))
            .Select(p => p.Origin).Where(h => h != origin.Id && h != destination.Id).Distinct().ToList();

        var found = new List<ConnectionResultDto>();
        foreach (var hub in hubs)
        {
            var first = await _flightRepository.SearchAsync(origin.Id, hub, fromUtc, toUtc, cancellationToken);
            if (first.Count == 0)
            {
                continue;
            }

            var second = await _flightRepository.SearchAsync(hub, destination.Id, fromUtc,
                toUtc.AddMinutes(ConnectionRules.MaxTransferMinutes).AddHours(24), cancellationToken);
            if (second.Count == 0)
            {
                continue;
            }

            var firstOk = (await FilterBookableAsync(first, cabinClass, passengers, cancellationToken)).Where(f => f.Route.Destination.IsActive).ToList();
            var secondOk = await FilterBookableAsync(second, cabinClass, passengers, cancellationToken);
            foreach (var a in firstOk)
            {
                foreach (var b in secondOk.Where(b => ConnectionRules.IsValidTransfer(a.ArrivalAt, b.DepartureAt)))
                {
                    var fares = a.Fares.Join(b.Fares, x => x.CabinClass, y => y.CabinClass, (x, y) => (x.CabinClass, Price: x.BasePrice + y.BasePrice))
                        .ToDictionary(x => x.CabinClass, x => x.Price);
                    if (fares.Count == 0 || (cabinClass is not null && !fares.ContainsKey(cabinClass.Value)))
                    {
                        continue;
                    }

                    found.Add(new ConnectionResultDto(
                        a.Route.Destination.Code, a.Route.Destination.City, (int)(b.DepartureAt - a.ArrivalAt).TotalMinutes,
                        (int)(b.ArrivalAt - a.DepartureAt).TotalMinutes, a.DepartureAt, b.ArrivalAt, fares,
                        new[] { a.ToResultDto(), b.ToResultDto() }));
                }
            }
        }

        return found.OrderBy(c => c.DurationMinutes).ThenBy(c => c.Fares.Values.Min()).ThenBy(c => c.DepartureAt)
            .Take(limit ?? ConnectionRules.MaxConnectionsPerSearch).ToList();
    }

    private async Task<Domain.Entities.Airport> GetAirportAsync(string code, CancellationToken cancellationToken)
    {
        var airport = await _airportRepository.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken);
        return airport is { IsActive: true }
            ? airport
            : throw new NotFoundException($"Airport '{code}' was not found.");
    }
}
