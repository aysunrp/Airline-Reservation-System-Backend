using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class PromoCodeRepository : BaseRepository<PromoCode>, IPromoCodeRepository
{
    public PromoCodeRepository(AppDbContext context) : base(context)
    {
    }

    public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(p => p.Code == code && p.Id != excludeId, cancellationToken);

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => Context.Set<Booking>().AnyAsync(b => b.PromoCodeId == id, cancellationToken);
}
