using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// In-memory failure counter (one instance for the whole API). It restarts empty with the API, which is fine for a pause of a few
/// minutes; the per-IP request limit in front of the sign-in endpoints bounds how many addresses one client can make it track.
/// </summary>
public class LoginAttemptTracker : ILoginAttemptTracker
{
    private const int CleanupThreshold = 5_000;

    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public LoginAttemptTracker(IOptions<AuthSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public bool IsLockedOut(string email, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!_entries.TryGetValue(Key(email), out var entry))
        {
            return false;
        }

        lock (entry)
        {
            var left = entry.LockedUntil - Now();
            if (left > TimeSpan.Zero)
            {
                retryAfter = left;
                return true;
            }
        }

        return false;
    }

    public bool RecordFailure(string email)
    {
        if (_entries.Count > CleanupThreshold)
        {
            RemoveExpired();
        }

        var now = Now();
        var window = TimeSpan.FromMinutes(Math.Max(1, _settings.LockoutMinutes));
        var entry = _entries.GetOrAdd(Key(email), _ => new Entry(now));

        lock (entry)
        {
            // Old failures stop counting once a full window passed without a new one.
            if (now - entry.LastFailureAt > window)
            {
                entry.Failures = 0;
                entry.LockedUntil = DateTime.MinValue;
            }

            entry.Failures += 1;
            entry.LastFailureAt = now;

            if (entry.Failures >= Math.Max(1, _settings.MaxFailedSignIns))
            {
                entry.LockedUntil = now + window;
                entry.Failures = 0;
                return true;
            }
        }

        return false;
    }

    public void Reset(string email) => _entries.TryRemove(Key(email), out _);

    private void RemoveExpired()
    {
        var now = Now();
        var window = TimeSpan.FromMinutes(Math.Max(1, _settings.LockoutMinutes));

        foreach (var pair in _entries)
        {
            if (now - pair.Value.LastFailureAt > window && pair.Value.LockedUntil <= now)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Key(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private sealed class Entry
    {
        public Entry(DateTime now) => LastFailureAt = now;

        public int Failures { get; set; }
        public DateTime LastFailureAt { get; set; }
        public DateTime LockedUntil { get; set; } = DateTime.MinValue;
    }
}
