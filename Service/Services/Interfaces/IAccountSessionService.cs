using Domain.Entities;

namespace Service.Services.Interfaces;

/// <summary>Makes a session for every sign-in and a token that carries it, so a session can be ended on its own.</summary>
public interface IAccountSessionService
{
    /// <summary>Records a session for the user (device and masked address from the request) and returns its token. Saves.</summary>
    Task<(string AccessToken, DateTime ExpiresAt)> IssueAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>True while the session of a token is open. A token without a session (made before sessions existed) is never asked about.</summary>
    Task<bool> IsOpenAsync(int userId, string sessionKey, CancellationToken cancellationToken = default);
}
