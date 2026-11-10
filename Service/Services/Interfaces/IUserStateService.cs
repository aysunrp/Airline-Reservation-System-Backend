using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

/// <summary>
/// What a signed token has to agree with on every request (the account exists, is active, has the claimed role, was not signed out since the
/// token was made). The answer is read fresh from the data layer and remembered for a few seconds, so the API's token check never needs the
/// repositories itself.
/// </summary>
public interface IUserStateService
{
    /// <summary>The state of the account; null when it does not exist.</summary>
    Task<UserStateDto?> GetAsync(int userId, CancellationToken cancellationToken = default);
}
