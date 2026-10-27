using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One security-relevant action: who did what to which object, and when (<see cref="BaseEntity.CreatedAt"/>). It never holds
/// passwords, tokens or other secrets, and it has no foreign key to the user so the trail survives a deleted account.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>The signed-in user who acted; null when nobody was signed in.</summary>
    public int? ActorUserId { get; set; }

    /// <summary>The role the actor had at that moment.</summary>
    public string? ActorRole { get; set; }

    /// <summary>What happened, e.g. UserRoleChanged (see AuditActions).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The kind of object it happened to, e.g. User or Flight.</summary>
    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    /// <summary>Short, non-sensitive context such as "Admin -> User".</summary>
    public string? Details { get; set; }
}
