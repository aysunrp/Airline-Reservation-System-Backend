using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IAuditLogRepository : IBaseRepository<AuditLog>
{
    /// <summary>Newest first, without change tracking; every filter is optional.</summary>
    Task<IReadOnlyList<AuditLog>> SearchAsync(string? action, int? actorUserId, string? targetType, int take, CancellationToken cancellationToken = default);
}
