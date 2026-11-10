using Domain.Enums;

namespace Service.Helpers.DTOs.Account;

public record AdminUserDto(int Id, string Email, string FirstName, string LastName, string Role, bool IsActive, bool EmailVerified, DateTime CreatedAt);

public record AuditLogDto(int Id, DateTime CreatedAt, int? ActorUserId, string? ActorRole, string Action, string TargetType, string? TargetId, string? Details);

/// <summary>A new Admin account; its e-mail address counts as confirmed because a SuperAdmin vouches for it.</summary>
public class CreateAdminRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>The first password; 8-100 characters with letters and digits. The Admin can change it by the forgot-password link.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Only User and Admin can be granted through the API; SuperAdmin is created on the server.</summary>
public class ChangeRoleRequestDto
{
    public UserRole Role { get; set; }
}

public class SetActiveRequestDto
{
    public bool IsActive { get; set; }
}
