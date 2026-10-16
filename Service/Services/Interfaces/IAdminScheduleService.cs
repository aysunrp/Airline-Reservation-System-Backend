using Service.Helpers.DTOs.Flight;

namespace Service.Services.Interfaces;

/// <summary>Admin "Flight Schedules" page: recurring flight plans (flight number, route, times, days of week, validity).</summary>
public interface IAdminScheduleService
{
    /// <summary>All schedules, or only the active ones that operate on the given date.</summary>
    Task<IReadOnlyList<ScheduleDto>> GetAllAsync(DateOnly? operatingOn = null, CancellationToken cancellationToken = default);

    Task<ScheduleDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ScheduleDto> CreateAsync(ScheduleRequestDto request, CancellationToken cancellationToken = default);

    Task<ScheduleDto> UpdateAsync(int id, ScheduleRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Flights created from the schedule are kept; they only lose the link to it.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
