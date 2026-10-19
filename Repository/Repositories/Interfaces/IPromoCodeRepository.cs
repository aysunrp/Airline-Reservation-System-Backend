using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IPromoCodeRepository : IBaseRepository<PromoCode>
{
    /// <summary>Returns a tracked promo code so usage can be incremented in the same unit of work.</summary>
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when any booking (in any status) was priced with the code.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default);
}
