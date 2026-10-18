using Service.Helpers.DTOs.Flight;

namespace Service.Services.Interfaces;

public interface IFlightGenerationService
{
    /// <summary>
    /// Creates the missing flights of active schedules between <paramref name="from"/> and <paramref name="to"/>
    /// (departure-airport dates, both included). Without dates it covers today and the configured days ahead.
    /// </summary>
    Task<FlightGenerationResultDto> GenerateAsync(int? scheduleId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}
