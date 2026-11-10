using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace Service.Services;

public class UserStateService : IUserStateService
{
    private readonly IUserRepository _users;
    private readonly IUserStateCache _cache;

    public UserStateService(IUserRepository users, IUserStateCache cache)
    {
        _users = users;
        _cache = cache;
    }

    public async Task<UserStateDto?> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGet(userId, out var state))
        {
            state = await _users.GetStateAsync(userId, cancellationToken);
            _cache.Set(userId, state);
        }

        return state is null ? null : new UserStateDto(state.Role, state.IsActive, state.SessionsValidFrom);
    }
}
