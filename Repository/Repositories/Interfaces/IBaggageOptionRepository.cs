using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IBaggageOptionRepository : IBaseRepository<BaggageOption>
{
    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when any ticket was sold with this baggage option.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
