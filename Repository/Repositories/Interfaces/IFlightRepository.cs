using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IFlightRepository : IBaseRepository<Flight>
{
    /// <summary>Non-cancelled flights on the given route departing in [fromUtc, toUtc), with fares and segments, ordered by departure.</summary>
    Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Returns a flight with route, aircraft, segments and fares (no tracking).</summary>
    Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>All flights with their details for the admin flights table, ordered by departure (no tracking).</summary>
    Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of flights for the admin list, oldest departure first. <paramref name="date"/> is the UTC departure day;
    /// the search matches the flight number, the cities and the aircraft model.
    /// </summary>
    Task<IReadOnlyList<Flight>> GetAdminPageAsync(string? search, FlightStatus? status, DateOnly? date, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Next non-cancelled departures from the given time (no tracking).</summary>
    Task<IReadOnlyList<Flight>> GetUpcomingAsync(DateTime from, int take, CancellationToken cancellationToken = default);

    /// <summary>A flight with route, aircraft, segments, fares and seats, tracked so it can be edited.</summary>
    Task<Flight?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string flightNumber, DateTime departureAt, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Flight number and departure of every flight departing in [fromUtc, toUtc).</summary>
    Task<IReadOnlyList<(string FlightNumber, DateTime DepartureAt)>> GetDepartureKeysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Operating days that already have a flight generated from the schedule, in [from, to].</summary>
    Task<IReadOnlyList<DateOnly>> GetScheduledDatesAsync(int scheduleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the flight with everything attached to it in one save. Returns false, and stores nothing, when a
    /// unique index rejects it (the same flight number and departure, or the same schedule and day, already exist);
    /// any other database error is thrown.
    /// </summary>
    Task<bool> TryAddAsync(Flight flight, CancellationToken cancellationToken = default);

    /// <summary>Tracked flights that have landed by <paramref name="now"/> and are not Cancelled or Completed yet.</summary>
    Task<IReadOnlyList<Flight>> GetLandedOpenAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Forgets every loaded entity, so long batch jobs do not keep thousands of saved rows in memory.</summary>
    void ClearTracking();

    /// <summary>True when any booking (in any status) contains the flight.</summary>
    Task<bool> HasBookingsAsync(int id, CancellationToken cancellationToken = default);
}
