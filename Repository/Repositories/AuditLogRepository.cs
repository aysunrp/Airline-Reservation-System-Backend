using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(string? action, int? actorUserId, string? targetType, int take, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var value = action.Trim();
            query = query.Where(a => a.Action == value);
        }

        if (actorUserId is not null)
        {
            query = query.Where(a => a.ActorUserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            var value = targetType.Trim();
            query = query.Where(a => a.TargetType == value);
        }

        return await query.OrderByDescending(a => a.Id).Take(take).ToListAsync(cancellationToken);
    }
}
