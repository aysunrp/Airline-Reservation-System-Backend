using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Users newest first, without change tracking; search matches the e-mail address or the name.</summary>
    Task<IReadOnlyList<User>> SearchAsync(string? search, UserRole? role, bool? isActive, int take, CancellationToken cancellationToken = default);

    /// <summary>How many active accounts have the role.</summary>
    Task<int> CountActiveInRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    Task<bool> HasBookingsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The ids of the active Admin and SuperAdmin accounts (support staff).</summary>
    Task<IReadOnlyList<int>> GetActiveStaffIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>The role and the active flag only, read fresh from the database; null when the account does not exist.</summary>
    Task<UserState?> GetStateAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>What a token has to agree with on every request.</summary>
public sealed record UserState(UserRole Role, bool IsActive);
