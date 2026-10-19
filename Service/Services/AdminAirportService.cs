using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminAirportService : IAdminAirportService
{
    private readonly IAirportRepository _airportRepository;
    private readonly IValidator<AirportRequestDto> _validator;

    public AdminAirportService(IAirportRepository airportRepository, IValidator<AirportRequestDto> validator)
    {
        _airportRepository = airportRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<AirportDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        return (await _airportRepository.GetAllAsync(cancellationToken))
            .Where(a => isActive is null || a.IsActive == isActive)
            .Where(a => string.IsNullOrEmpty(term)
                        || a.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.City.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.CountryCode.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Code)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<AirportDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => (await GetAirportAsync(id, cancellationToken)).ToDto();

    public async Task<AirportDto> CreateAsync(AirportRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeIsFreeAsync(code, null, cancellationToken);

        var airport = new Airport();
        Apply(request, code, airport);

        await _airportRepository.AddAsync(airport, cancellationToken);
        await _airportRepository.SaveChangesAsync(cancellationToken);
        return airport.ToDto();
    }

    public async Task<AirportDto> UpdateAsync(int id, AirportRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var airport = await GetAirportAsync(id, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeIsFreeAsync(code, id, cancellationToken);

        Apply(request, code, airport);
        _airportRepository.Update(airport);
        await _airportRepository.SaveChangesAsync(cancellationToken);
        return airport.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var airport = await GetAirportAsync(id, cancellationToken);
        if (await _airportRepository.IsInUseAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("This airport is used by routes, schedules or flights. Set it to inactive instead of deleting it.");
        }

        _airportRepository.Delete(airport);
        await _airportRepository.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(AirportRequestDto request, string code, Airport airport)
    {
        airport.Code = code;
        airport.Name = request.Name.Trim();
        airport.City = request.City.Trim();
        airport.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        airport.TimeZoneId = request.TimeZoneId.Trim();
        airport.IsActive = request.IsActive;
    }

    private async Task EnsureCodeIsFreeAsync(string code, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _airportRepository.CodeExistsAsync(code, excludeId, cancellationToken))
        {
            throw new ConflictException($"An airport with the code {code} already exists.");
        }
    }

    private async Task<Airport> GetAirportAsync(int id, CancellationToken cancellationToken)
        => await _airportRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Airport not found.");
}
