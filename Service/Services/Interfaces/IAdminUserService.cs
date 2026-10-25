using Domain.Enums;
using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

/// <summary>Account and role management. Only a SuperAdmin reaches it; every change is written to the audit log.</summary>
public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserDto>> GetAllAsync(string? search, UserRole? role, bool? isActive, int take, CancellationToken cancellationToken = default);

    Task<AdminUserDto> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates an Admin account. SuperAdmin accounts cannot be created here.</summary>
    Task<AdminUserDto> CreateAdminAsync(AuditActor actor, CreateAdminRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Grants or takes back the Admin role (User or Admin). The role of a SuperAdmin is never changed here.</summary>
    Task<AdminUserDto> ChangeRoleAsync(AuditActor actor, int id, UserRole role, CancellationToken cancellationToken = default);

    Task<AdminUserDto> SetActiveAsync(AuditActor actor, int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Deletes an account that has no bookings; an account with bookings is deactivated instead (422).</summary>
    Task DeleteAsync(AuditActor actor, int id, CancellationToken cancellationToken = default);
}
