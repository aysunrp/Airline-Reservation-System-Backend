using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Aircraft;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seed;
using Service.Helpers.Exceptions;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Adds the demo network from <see cref="DemoSeedData"/>. Every record is looked up by its natural key first
/// (airport code, registration, route airports, flight number on a route, option code, promo code, cabin and band)
/// and left untouched when it exists, so the seed can run any number of times and never overwrites changes made
/// in the admin panel. Aircraft, routes and schedules go through the admin services, so they get the same
/// validation, seat layout and fare checks as records created by hand.
/// </summary>
public class DemoDataSeeder : IDemoDataSeeder
{
    private readonly IAirportRepository _airportRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly IBaseRepository<BaggageOption> _baggageRepository;
    private readonly IBaseRepository<MealOption> _mealRepository;
    private readonly IBaseRepository<PromoCode> _promoCodeRepository;
    private readonly IBaseRepository<CancellationRule> _cancellationRuleRepository;
    private readonly IAdminAircraftService _aircraftService;
    private readonly IAdminRouteService _routeService;
    private readonly IAdminScheduleService _scheduleService;
    private readonly IFlightGenerationService _generationService;

    public DemoDataSeeder(
        IAirportRepository airportRepository,
        IAircraftRepository aircraftRepository,
        IRouteRepository routeRepository,
        IFlightScheduleRepository scheduleRepository,
        IBaseRepository<BaggageOption> baggageRepository,
        IBaseRepository<MealOption> mealRepository,
        IBaseRepository<PromoCode> promoCodeRepository,
        IBaseRepository<CancellationRule> cancellationRuleRepository,
        IAdminAircraftService aircraftService,
        IAdminRouteService routeService,
        IAdminScheduleService scheduleService,
        IFlightGenerationService generationService)
    {
        _airportRepository = airportRepository;
        _aircraftRepository = aircraftRepository;
        _routeRepository = routeRepository;
        _scheduleRepository = scheduleRepository;
        _baggageRepository = baggageRepository;
        _mealRepository = mealRepository;
        _promoCodeRepository = promoCodeRepository;
        _cancellationRuleRepository = cancellationRuleRepository;
        _aircraftService = aircraftService;
        _routeService = routeService;
        _scheduleService = scheduleService;
        _generationService = generationService;
    }

    public async Task<DemoSeedResultDto> SeedAsync(bool generateFlights = true, CancellationToken cancellationToken = default)
    {
        var run = new Run();

        await SeedAirportsAsync(run, cancellationToken);
        await SeedAircraftAsync(run, cancellationToken);
        await SeedRoutesAsync(run, cancellationToken);
        await SeedSchedulesAsync(run, cancellationToken);
        await SeedBookingOptionsAsync(run, cancellationToken);
        await SeedPromoCodesAsync(run, cancellationToken);
        await SeedCancellationRulesAsync(run, cancellationToken);

        var flights = generateFlights ? await _generationService.GenerateAsync(cancellationToken: cancellationToken) : null;
        return new DemoSeedResultDto(run.Added, run.AlreadyPresent, run.Skipped, flights);
    }

    private async Task SeedAirportsAsync(Run run, CancellationToken cancellationToken)
    {
        foreach (var seed in DemoSeedData.Airports)
        {
            if (await _airportRepository.GetByCodeAsync(seed.Code, cancellationToken) is not null)
            {
                run.Present("Airports");
                continue;
            }

            await _airportRepository.AddAsync(new Airport
            {
                Code = seed.Code, Name = seed.Name, City = seed.City, CountryCode = seed.CountryCode, TimeZoneId = seed.TimeZoneId
            }, cancellationToken);
            run.Add("Airports");
        }

        await _airportRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAircraftAsync(Run run, CancellationToken cancellationToken)
    {
        foreach (var seed in DemoSeedData.Aircraft)
        {
            if (await _aircraftRepository.RegistrationExistsAsync(seed.RegistrationNumber, null, cancellationToken))
            {
                run.Present("Aircraft");
                continue;
            }

            await run.TryAsync("Aircraft", $"Aircraft {seed.RegistrationNumber}", () => _aircraftService.CreateAsync(new AircraftRequestDto
            {
                Name = seed.Name, Model = seed.Model, RegistrationNumber = seed.RegistrationNumber, Status = AircraftStatus.Active,
                BusinessSeats = seed.BusinessSeats, ComfortSeats = seed.ComfortSeats, EconomySeats = seed.EconomySeats
            }, cancellationToken));
        }
    }

    private async Task SeedRoutesAsync(Run run, CancellationToken cancellationToken)
    {
        foreach (var seed in DemoSeedData.Routes)
        {
            if (await FindRouteAsync(seed.OriginCode, seed.DestinationCode, cancellationToken) is not null)
            {
                run.Present("Routes");
                continue;
            }

            await run.TryAsync("Routes", $"Route {seed.OriginCode}-{seed.DestinationCode}", () => _routeService.CreateAsync(new RouteRequestDto
            {
                OriginCode = seed.OriginCode, DestinationCode = seed.DestinationCode, DistanceKm = seed.DistanceKm, DurationMinutes = seed.DurationMinutes
            }, cancellationToken));
        }
    }

    private async Task SeedSchedulesAsync(Run run, CancellationToken cancellationToken)
    {
        var existing = (await _scheduleRepository.GetAllWithDetailsAsync(cancellationToken))
            .Select(s => (s.FlightNumber, s.RouteId))
            .ToHashSet();
        var aircraft = (await _aircraftRepository.GetAllAsync(cancellationToken)).ToDictionary(a => a.RegistrationNumber, a => a.Id);

        foreach (var seed in DemoSeedData.Schedules)
        {
            var label = $"Schedule {seed.FlightNumber} {seed.OriginCode}-{seed.DestinationCode}";
            var route = await FindRouteAsync(seed.OriginCode, seed.DestinationCode, cancellationToken);
            if (route is null)
            {
                run.Skip($"{label}: the route does not exist.");
                continue;
            }

            if (existing.Contains((seed.FlightNumber, route.Id)))
            {
                run.Present("Schedules");
                continue;
            }

            if (!aircraft.TryGetValue(seed.AircraftRegistration, out var aircraftId))
            {
                run.Skip($"{label}: aircraft {seed.AircraftRegistration} does not exist.");
                continue;
            }

            var request = new ScheduleRequestDto
            {
                FlightNumber = seed.FlightNumber, RouteId = route.Id, DefaultAircraftId = aircraftId,
                DepartureTime = seed.Departure, ArrivalTime = seed.Arrival, ArrivalDayOffset = seed.ArrivalDayOffset,
                DaysOfWeek = ScheduleDays.Everyday, ValidFrom = DemoSeedData.ScheduleValidFrom,
                Fares =
                {
                    new ScheduleFareRequestDto { CabinClass = CabinClass.Economy, BasePrice = seed.Economy },
                    new ScheduleFareRequestDto { CabinClass = CabinClass.Comfort, BasePrice = seed.Comfort },
                    new ScheduleFareRequestDto { CabinClass = CabinClass.Business, BasePrice = seed.Business }
                },
                Stops = (seed.Stops ?? Array.Empty<StopSeed>())
                    .Select((stop, index) => new ScheduleStopRequestDto
                    {
                        SequenceNo = index + 1, AirportCode = stop.AirportCode,
                        ArrivalOffsetMinutes = stop.ArrivalOffsetMinutes, LayoverMinutes = stop.LayoverMinutes
                    })
                    .ToList()
            };
            await run.TryAsync("Schedules", label, () => _scheduleService.CreateAsync(request, cancellationToken));
        }
    }

    private async Task SeedBookingOptionsAsync(Run run, CancellationToken cancellationToken)
    {
        var baggageCodes = (await _baggageRepository.GetAllAsync(cancellationToken)).Select(o => o.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DemoSeedData.BaggageOptions)
        {
            if (!baggageCodes.Contains(seed.Code))
            {
                await _baggageRepository.AddAsync(new BaggageOption { Code = seed.Code, Name = seed.Name, WeightKg = seed.WeightKg, Price = seed.Price }, cancellationToken);
                run.Add("BaggageOptions");
            }
            else
            {
                run.Present("BaggageOptions");
            }
        }

        var mealCodes = (await _mealRepository.GetAllAsync(cancellationToken)).Select(o => o.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DemoSeedData.MealOptions)
        {
            if (!mealCodes.Contains(seed.Code))
            {
                await _mealRepository.AddAsync(new MealOption { Code = seed.Code, Name = seed.Name, Description = seed.Description, Price = seed.Price }, cancellationToken);
                run.Add("MealOptions");
            }
            else
            {
                run.Present("MealOptions");
            }
        }

        await _mealRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPromoCodesAsync(Run run, CancellationToken cancellationToken)
    {
        var codes = (await _promoCodeRepository.GetAllAsync(cancellationToken)).Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, discountPercent) in DemoSeedData.PromoCodes)
        {
            if (codes.Contains(code))
            {
                run.Present("PromoCodes");
                continue;
            }

            await _promoCodeRepository.AddAsync(new PromoCode { Code = code, DiscountPercent = discountPercent }, cancellationToken);
            run.Add("PromoCodes");
        }

        await _promoCodeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCancellationRulesAsync(Run run, CancellationToken cancellationToken)
    {
        var bands = (await _cancellationRuleRepository.GetAllAsync(cancellationToken))
            .Select(r => (r.CabinClass, r.MinDaysBeforeDeparture))
            .ToHashSet();
        foreach (var seed in DemoSeedData.CancellationRules)
        {
            if (bands.Contains((seed.CabinClass, seed.MinDaysBeforeDeparture)))
            {
                run.Present("CancellationRules");
                continue;
            }

            await _cancellationRuleRepository.AddAsync(new CancellationRule
            {
                CabinClass = seed.CabinClass, MinDaysBeforeDeparture = seed.MinDaysBeforeDeparture, FeePercent = seed.FeePercent
            }, cancellationToken);
            run.Add("CancellationRules");
        }

        await _cancellationRuleRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Route?> FindRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken)
    {
        var origin = await _airportRepository.GetByCodeAsync(originCode, cancellationToken);
        var destination = await _airportRepository.GetByCodeAsync(destinationCode, cancellationToken);
        return origin is null || destination is null
            ? null
            : await _routeRepository.GetByAirportsAsync(origin.Id, destination.Id, cancellationToken);
    }

    private sealed class Run
    {
        public Dictionary<string, int> Added { get; } = new();
        public Dictionary<string, int> AlreadyPresent { get; } = new();
        public List<string> Skipped { get; } = new();

        public void Add(string kind) => Added[kind] = Added.GetValueOrDefault(kind) + 1;

        public void Present(string kind) => AlreadyPresent[kind] = AlreadyPresent.GetValueOrDefault(kind) + 1;

        public void Skip(string reason) => Skipped.Add(reason);

        /// <summary>Creates a record through an admin service; a rule that rejects it is reported instead of stopping the seed.</summary>
        public async Task TryAsync<T>(string kind, string label, Func<Task<T>> create)
        {
            try
            {
                await create();
                Add(kind);
            }
            catch (Exception ex) when (ex is ValidationException or BusinessRuleException or ConflictException or NotFoundException)
            {
                Skip($"{label}: {ex.Message}");
            }
        }
    }
}
