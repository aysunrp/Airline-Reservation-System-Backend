using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class MealOptionRepository : BaseRepository<MealOption>, IMealOptionRepository
{
    public MealOptionRepository(AppDbContext context) : base(context)
    {
    }

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Set.AnyAsync(o => o.Code == code && o.Id != excludeId, cancellationToken);

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => Context.Set<Ticket>().AnyAsync(t => t.MealOptionId == id, cancellationToken);
}
