using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class UserSessionRepository : BaseRepository<UserSession>, IUserSessionRepository
{
    public UserSessionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<UserSession>> GetOpenAsync(int userId, DateTime now, CancellationToken cancellationToken = default)
        => await Set.Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);

    public Task<UserSession?> GetByKeyAsync(string sessionKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(s => s.SessionKey == sessionKey, cancellationToken);

    public Task<bool> IsOpenAsync(int userId, string sessionKey, DateTime now, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().AnyAsync(s => s.UserId == userId && s.SessionKey == sessionKey && s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);

    public async Task<DateTime?> GetLatestSignInAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt)
            .Select(s => (DateTime?)s.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteOldAsync(DateTime before, CancellationToken cancellationToken = default)
        => Set.Where(s => s.ExpiresAt < before || (s.RevokedAt != null && s.RevokedAt < before)).ExecuteDeleteAsync(cancellationToken);
}
