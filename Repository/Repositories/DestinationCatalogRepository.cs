using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class DestinationCatalogRepository : BaseRepository<Country>, IDestinationCatalogRepository
{
    public DestinationCatalogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Country>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(c => c.Cities).ThenInclude(city => city.Airports)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<Country?> GetCountryAsync(int id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<DestinationCity?> GetCityAsync(int id, CancellationToken cancellationToken = default)
        => Context.Set<DestinationCity>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Airport>> GetAirportsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
        => await Context.Set<Airport>().Where(a => codes.Contains(a.Code)).ToListAsync(cancellationToken);

    public async Task AddCityAsync(DestinationCity city, CancellationToken cancellationToken = default)
        => await Context.Set<DestinationCity>().AddAsync(city, cancellationToken);

    public async Task AddAirportAsync(Airport airport, CancellationToken cancellationToken = default)
        => await Context.Set<Airport>().AddAsync(airport, cancellationToken);

    public async Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await Context.Set<Route>().AsNoTracking().Where(r => r.IsActive)
            .Select(r => new { r.OriginAirportId, r.DestinationAirportId })
            .ToListAsync(cancellationToken);
        return routes.SelectMany(r => new[] { r.OriginAirportId, r.DestinationAirportId }).ToHashSet();
    }

    public Task<DestinationCity?> FindCityAsync(string countryCode, string cityName, CancellationToken cancellationToken = default)
        => Context.Set<DestinationCity>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Country.Code == countryCode && c.Name == cityName, cancellationToken);
}
