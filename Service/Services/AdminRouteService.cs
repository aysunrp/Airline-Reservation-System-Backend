using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminRouteService : IAdminRouteService
{
    private readonly IRouteRepository _routeRepository;
    private readonly IAirportRepository _airportRepository;
    private readonly IValidator<RouteRequestDto> _validator;

    public AdminRouteService(IRouteRepository routeRepository, IAirportRepository airportRepository, IValidator<RouteRequestDto> validator)
    {
        _routeRepository = routeRepository;
        _airportRepository = airportRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<RouteDto>> GetAllAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        var routes = (await _routeRepository.GetAllWithAirportsAsync(cancellationToken)).Select(r => r.ToDto());

        return routes
            .Where(r => string.IsNullOrEmpty(term)
                        || r.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || r.OriginCity.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || r.DestinationCity.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<RouteDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => (await GetRouteAsync(id, cancellationToken)).ToDto();

    public async Task<RouteDto> CreateAsync(RouteRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var (origin, destination) = await GetAirportsAsync(request, cancellationToken);

        if (await _routeRepository.ExistsAsync(origin.Id, destination.Id, null, cancellationToken))
        {
            throw new ConflictException($"The route {origin.Code}-{destination.Code} already exists.");
        }

        var route = new Route
        {
            OriginAirportId = origin.Id,
            Origin = origin,
            DestinationAirportId = destination.Id,
            Destination = destination
        };
        Apply(request, route);

        await _routeRepository.AddAsync(route, cancellationToken);
        await _routeRepository.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }

    public async Task<RouteDto> UpdateAsync(int id, RouteRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var route = await GetRouteAsync(id, cancellationToken);
        var (origin, destination) = await GetAirportsAsync(request, cancellationToken);

        if (origin.Id != route.OriginAirportId || destination.Id != route.DestinationAirportId)
        {
            if (await _routeRepository.IsInUseAsync(id, cancellationToken))
            {
                throw new BusinessRuleException("The airports of a route used by flights or schedules cannot be changed.");
            }

            if (await _routeRepository.ExistsAsync(origin.Id, destination.Id, id, cancellationToken))
            {
                throw new ConflictException($"The route {origin.Code}-{destination.Code} already exists.");
            }

            route.OriginAirportId = origin.Id;
            route.Origin = origin;
            route.DestinationAirportId = destination.Id;
            route.Destination = destination;
        }

        Apply(request, route);
        _routeRepository.Update(route);
        await _routeRepository.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(id, cancellationToken);
        if (await _routeRepository.IsInUseAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("This route is used by flights or schedules. Set it to Inactive instead of deleting it.");
        }

        _routeRepository.Delete(route);
        await _routeRepository.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(RouteRequestDto request, Route route)
    {
        route.DistanceKm = request.DistanceKm;
        route.DurationMinutes = request.DurationMinutes;
        route.IsActive = request.IsActive;
    }

    private async Task<(Airport Origin, Airport Destination)> GetAirportsAsync(RouteRequestDto request, CancellationToken cancellationToken)
    {
        async Task<Airport> Find(string code)
            => await _airportRepository.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
               ?? throw new NotFoundException($"Airport '{code}' was not found.");

        return (await Find(request.OriginCode), await Find(request.DestinationCode));
    }

    private async Task<Route> GetRouteAsync(int id, CancellationToken cancellationToken)
        => await _routeRepository.GetWithAirportsAsync(id, cancellationToken)
           ?? throw new NotFoundException("Route not found.");
}
