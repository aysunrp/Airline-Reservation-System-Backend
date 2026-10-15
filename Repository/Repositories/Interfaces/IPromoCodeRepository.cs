using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IPromoCodeRepository : IBaseRepository<PromoCode>
{
    /// <summary>Returns a tracked promo code so usage can be incremented in the same unit of work.</summary>
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
