using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class SavedPassengerRepository : BaseRepository<SavedPassenger>, ISavedPassengerRepository
{
    public SavedPassengerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<SavedPassenger>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync(cancellationToken);
}
