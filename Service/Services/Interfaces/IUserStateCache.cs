using Repository.Repositories.Interfaces;

namespace Service.Services.Interfaces;

/// <summary>
/// Remembers for a few seconds the role and active flag of the accounts that sent tokens, so checking a token against the
/// database on every request stays cheap. Whoever changes a role or deactivates an account must call <see cref="Invalidate"/>.
/// </summary>
public interface IUserStateCache
{
    /// <summary>True when the state of the account is remembered; <paramref name="state"/> is null for an account that does not exist.</summary>
    bool TryGet(int userId, out UserState? state);

    void Set(int userId, UserState? state);

    void Invalidate(int userId);
}
