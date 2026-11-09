using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class UserStateCache : IUserStateCache
{
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<int, (UserState? State, DateTime ExpiresAt)> _entries = new();
    private readonly ConcurrentDictionary<(int UserId, string Key), (bool Open, DateTime ExpiresAt)> _sessions = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public UserStateCache(IOptions<AuthSettings> settings, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lifetime = TimeSpan.FromSeconds(Math.Max(0, settings.Value.UserStateCacheSeconds));
    }

    public bool TryGet(int userId, out UserState? state)
    {
        state = null;
        if (!_entries.TryGetValue(userId, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            _entries.TryRemove(userId, out _);
            return false;
        }

        state = entry.State;
        return true;
    }

    public void Set(int userId, UserState? state)
    {
        if (_lifetime <= TimeSpan.Zero)
        {
            return;
        }

        // A flood of tokens for accounts that do not exist must not grow the memory without limit.
        if (_entries.Count >= MaxEntries)
        {
            _entries.Clear();
        }

        _entries[userId] = (state, _timeProvider.GetUtcNow().UtcDateTime + _lifetime);
    }

    public void Invalidate(int userId)
    {
        _entries.TryRemove(userId, out _);
        foreach (var key in _sessions.Keys.Where(k => k.UserId == userId).ToList())
        {
            _sessions.TryRemove(key, out _);
        }
    }

    public bool TryGetSession(int userId, string sessionKey, out bool open)
    {
        open = false;
        if (!_sessions.TryGetValue((userId, sessionKey), out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            _sessions.TryRemove((userId, sessionKey), out _);
            return false;
        }

        open = entry.Open;
        return true;
    }

    public void SetSession(int userId, string sessionKey, bool open)
    {
        if (_lifetime <= TimeSpan.Zero)
        {
            return;
        }

        if (_sessions.Count >= MaxEntries)
        {
            _sessions.Clear();
        }

        _sessions[(userId, sessionKey)] = (open, _timeProvider.GetUtcNow().UtcDateTime + _lifetime);
    }
}
