using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IFlightScheduleRepository : IBaseRepository<FlightSchedule>
{
    /// <summary>All schedules with route airports and default aircraft, ordered by flight number (no tracking).</summary>
    Task<IReadOnlyList<FlightSchedule>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>One schedule with route airports and default aircraft, tracked so it can be edited.</summary>
    Task<FlightSchedule?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True when the schedule has produced at least one flight (past or future, booked or not).</summary>
    Task<bool> HasFlightsAsync(int id, CancellationToken cancellationToken = default);
}
