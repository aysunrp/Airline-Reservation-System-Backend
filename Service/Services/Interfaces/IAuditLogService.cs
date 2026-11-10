using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

/// <summary>Who did something: the signed-in user's id and role, or nothing when nobody was signed in.</summary>
public sealed record AuditActor(int? UserId, string? Role);

public interface IAuditLogService
{
    /// <summary>
    /// Adds the entry to the unit of work without saving it, so it is committed together with the change it describes
    /// (the next SaveChanges of the same request).
    /// </summary>
    Task StageAsync(AuditActor actor, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves one entry on its own. The action already happened, so a failure to write it is logged and never turned into an
    /// error for the person who did it.
    /// </summary>
    Task RecordAsync(AuditActor actor, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetAsync(string? action, int? actorUserId, string? targetType, int take, CancellationToken cancellationToken = default);
}
