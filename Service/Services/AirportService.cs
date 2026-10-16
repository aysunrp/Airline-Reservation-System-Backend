using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Airport;
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
        return airports.Select(a => a.ToDto()).ToList();
    }
}
