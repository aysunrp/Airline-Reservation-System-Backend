using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAircraftRepository : IBaseRepository<Aircraft>
{
    /// <summary>All aircraft with their seats (for the per-cabin seat counts), ordered by name (no tracking).</summary>
    Task<IReadOnlyList<Aircraft>> GetAllWithSeatsAsync(CancellationToken cancellationToken = default);

    /// <summary>One aircraft with its seats, tracked so the layout can be changed.</summary>
    Task<Aircraft?> GetWithSeatsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> RegistrationExistsAsync(string registrationNumber, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when flights or flight schedules use the aircraft.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
