using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class NotificationPreferenceRepository : BaseRepository<NotificationPreference>, INotificationPreferenceRepository
{
    public NotificationPreferenceRepository(AppDbContext context) : base(context)
    {
    }

    public Task<NotificationPreference?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task<IReadOnlySet<string>> GetEmailsWithoutMarketingAsync(CancellationToken cancellationToken = default)
        => (await Set.AsNoTracking().Where(p => !p.Marketing).Select(p => p.User.Email).ToListAsync(cancellationToken))
            .Select(e => e.ToLowerInvariant()).ToHashSet();
}
