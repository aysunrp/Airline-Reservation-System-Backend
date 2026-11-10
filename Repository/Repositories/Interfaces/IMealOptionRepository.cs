using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IMealOptionRepository : IBaseRepository<MealOption>
{
    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when any ticket was sold with this meal option.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
