using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Destinations;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class DestinationCatalogService : IDestinationCatalogService
{
    private readonly IDestinationCatalogRepository _catalog;
    private readonly IValidator<DestinationCountryRequestDto> _countryValidator;
    private readonly IValidator<DestinationCityRequestDto> _cityValidator;

    public DestinationCatalogService(
        IDestinationCatalogRepository catalog, IValidator<DestinationCountryRequestDto> countryValidator, IValidator<DestinationCityRequestDto> cityValidator)
    {
        _catalog = catalog;
        _countryValidator = countryValidator;
        _cityValidator = cityValidator;
    }

    public async Task<IReadOnlyList<DestinationCountryDto>> GetPublicAsync(CancellationToken cancellationToken = default)
        => (await BuildAsync(cancellationToken))
            .Where(c => c.IsActive)
            .Select(c => c with
            {
                Cities = c.Cities.Where(city => city.IsActive).Select(city => city with { Airports = city.Airports.Where(a => a.IsActive).ToList() }).ToList()
            })
            .ToList();

    public async Task<DestinationCountryDto> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default)
        => (await GetPublicAsync(cancellationToken)).FirstOrDefault(c => c.Id == id && !c.Cities.Any(city => city.IsHub))
           ?? throw new NotFoundException("Destination not found.");

    public async Task<DestinationCountryDto> UpdateCountryAsync(int id, DestinationCountryRequestDto request, CancellationToken cancellationToken = default)
    {
        await _countryValidator.ValidateAndThrowAsync(request, cancellationToken);
        var country = await _catalog.GetCountryAsync(id, cancellationToken) ?? throw new NotFoundException("Country not found.");

        country.Name = request.Name.Trim();
        country.NameAz = request.NameAz.Trim();
        country.NameRu = request.NameRu.Trim();
        country.Description = request.Description.Trim();
        country.DescriptionAz = request.DescriptionAz.Trim();
        country.DescriptionRu = request.DescriptionRu.Trim();
        _catalog.Update(country);
        await _catalog.SaveChangesAsync(cancellationToken);
        return (await BuildAsync(cancellationToken)).Single(c => c.Id == id);
    }

    public async Task<DestinationCountryDto> UpdateCityAsync(int id, DestinationCityRequestDto request, CancellationToken cancellationToken = default)
    {
        await _cityValidator.ValidateAndThrowAsync(request, cancellationToken);
        var city = await _catalog.GetCityAsync(id, cancellationToken) ?? throw new NotFoundException("City not found.");

        city.NameAz = request.NameAz.Trim();
        city.NameRu = request.NameRu.Trim();
        city.Aliases = string.IsNullOrWhiteSpace(request.Aliases) ? null : string.Join(';', SplitAliases(request.Aliases));
        city.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(cancellationToken);
        return (await BuildAsync(cancellationToken)).Single(c => c.Id == city.CountryId);
    }

    public Task<IReadOnlyList<DestinationCountryDto>> GetAllAsync(CancellationToken cancellationToken = default) => BuildAsync(cancellationToken);

    public async Task<DestinationCountryDto> SetCountryActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var country = await _catalog.GetCountryAsync(id, cancellationToken) ?? throw new NotFoundException("Country not found.");
        country.IsActive = isActive;
        _catalog.Update(country);
        await _catalog.SaveChangesAsync(cancellationToken);
        return (await BuildAsync(cancellationToken)).Single(c => c.Id == id);
    }

    public async Task<DestinationCountryDto> SetCityActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var city = await _catalog.GetCityAsync(id, cancellationToken) ?? throw new NotFoundException("City not found.");
        city.IsActive = isActive;
        city.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(cancellationToken);
        return (await BuildAsync(cancellationToken)).Single(c => c.Id == city.CountryId);
    }

    private async Task<IReadOnlyList<DestinationCountryDto>> BuildAsync(CancellationToken cancellationToken)
    {
        var countries = await _catalog.GetCatalogAsync(cancellationToken);
        var withRoutes = await _catalog.GetAirportIdsWithActiveRoutesAsync(cancellationToken);

        return countries.Select(c => new DestinationCountryDto(
            c.Id, c.Code, c.Name, c.NameAz, c.NameRu, c.Description, c.DescriptionAz, c.DescriptionRu, c.ImageFile, c.SortOrder, c.IsActive,
            c.Cities.OrderBy(city => city.SortOrder).ThenBy(city => city.Name).Select(city => new DestinationCityDto(
                city.Id, city.Name, city.NameAz, city.NameRu, SplitAliases(city.Aliases), city.IsActive, city.IsHub,
                city.Airports.OrderBy(a => a.Code).Select(a => new DestinationAirportDto(a.Id, a.Code, a.Name, a.TimeZoneId, a.IsActive, withRoutes.Contains(a.Id))).ToList()))
            .ToList())).ToList();
    }

    internal static IReadOnlyList<string> SplitAliases(string? aliases)
        => string.IsNullOrWhiteSpace(aliases)
            ? Array.Empty<string>()
            : aliases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
