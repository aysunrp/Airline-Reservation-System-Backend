using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IUserSessionRepository : IBaseRepository<UserSession>
{
    /// <summary>The sessions of the account that are not revoked and not expired, newest first (tracked).</summary>
    Task<IReadOnlyList<UserSession>> GetOpenAsync(int userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The session with this key; null when there is none (tracked).</summary>
    Task<UserSession?> GetByKeyAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>True while the session exists for the account, is not revoked and has not expired. Read on every request, so it is cached by the caller.</summary>
    Task<bool> IsOpenAsync(int userId, string sessionKey, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The latest sign-in moment of the account (any session, open or not); null when none is recorded.</summary>
    Task<DateTime?> GetLatestSignInAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Removes sessions that expired or were revoked before the given moment; returns how many.</summary>
    Task<int> DeleteOldAsync(DateTime before, CancellationToken cancellationToken = default);
}
