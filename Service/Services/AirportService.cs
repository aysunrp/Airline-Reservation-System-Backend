using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AirportService : IAirportService
{
    private readonly IAirportRepository _airportRepository;

    public AirportService(IAirportRepository airportRepository)
    {
        _airportRepository = airportRepository;
    }

    public async Task<IReadOnlyList<AirportDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var airports = await _airportRepository.GetActiveAsync(cancellationToken);
        var withRoutes = await _airportRepository.GetAirportIdsWithActiveRoutesAsync(cancellationToken);
        return airports.Select(a => a.ToPickerDto(withRoutes.Contains(a.Id))).ToList();
    }

    public async Task<IReadOnlyList<RoutePairDto>> GetNetworkAsync(CancellationToken cancellationToken = default)
        => (await _airportRepository.GetActiveRoutePairsAsync(cancellationToken)).Select(p => new RoutePairDto(p.Origin, p.Destination)).ToList();

    public async Task<RouteCheckDto> CheckRouteAsync(string originCode, string destinationCode, CancellationToken cancellationToken = default)
    {
        var origin = (originCode ?? string.Empty).Trim().ToUpperInvariant();
        var destination = (destinationCode ?? string.Empty).Trim().ToUpperInvariant();
        if (origin.Length != 3 || destination.Length != 3)
        {
            throw new BusinessRuleException("Üç hərfli hava limanı kodlarından istifadə edin.");
        }

        if (origin == destination)
        {
            throw new BusinessRuleException("Gediş və təyinat eynidir.");
        }

        var first = await _airportRepository.GetByCodeAsync(origin, cancellationToken) ?? throw new NotFoundException($"'{origin}' hava limanı tapılmadı.");
        var second = await _airportRepository.GetByCodeAsync(destination, cancellationToken) ?? throw new NotFoundException($"'{destination}' hava limanı tapılmadı.");
        // A route exists when a flight joins the two airports or one transfer does: Baku - Santorini has no direct flight but is bookable through Athens.
        var direct = await _airportRepository.HasActiveRouteAsync(first.Code, second.Code, cancellationToken);
        var exists = direct || await _airportRepository.HasActiveConnectionAsync(first.Code, second.Code, cancellationToken);
        return new RouteCheckDto(first.Code, second.Code, exists, direct);
    }
}
