using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace Service.Services;

public class AccountSessionService : IAccountSessionService
{
    private readonly IUserSessionRepository _sessions;
    private readonly ITokenService _tokens;
    private readonly IUserStateCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly IClientInfoAccessor? _client;

    public AccountSessionService(IUserSessionRepository sessions, ITokenService tokens, IUserStateCache cache, TimeProvider timeProvider, IClientInfoAccessor? client = null)
    {
        _sessions = sessions;
        _tokens = tokens;
        _cache = cache;
        _timeProvider = timeProvider;
        _client = client;
    }

    public async Task<(string AccessToken, DateTime ExpiresAt)> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var (token, expiresAt) = _tokens.CreateToken(user, key);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Old closed sessions are cleaned up now and then, so the table does not grow without end.
        await _sessions.DeleteOldAsync(now.AddDays(-30), cancellationToken);
        await _sessions.AddAsync(new UserSession
        {
            UserId = user.Id,
            SessionKey = key,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Device = DescribeDevice(_client?.UserAgent),
            MaskedIp = MaskIp(_client?.IpAddress)
        }, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);
        return (token, expiresAt);
    }

    public async Task<bool> IsOpenAsync(int userId, string sessionKey, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetSession(userId, sessionKey, out var open))
        {
            return open;
        }

        open = await _sessions.IsOpenAsync(userId, sessionKey, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        _cache.SetSession(userId, sessionKey, open);
        return open;
    }

    /// <summary>"Chrome, Windows": enough for the person to recognise the device, nothing that identifies it.</summary>
    internal static string? DescribeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var ua = userAgent;
        var browser = ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Opera", StringComparison.OrdinalIgnoreCase) ? "Opera"
            : ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) || ua.Contains("CriOS", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : null;
        var system = ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : null;

        var text = string.Join(", ", new[] { browser, system }.Where(p => p is not null));
        return text.Length == 0 ? null : text;
    }

    /// <summary>The address with its last part hidden: "203.0.113.x" or "2001:db8:1::x".</summary>
    internal static string? MaskIp(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address, out var ip))
        {
            return null;
        }

        if (IPAddress.IsLoopback(ip))
        {
            return "localhost";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return $"{b[0]}.{b[1]}.{b[2]}.x";
        }

        var groups = ip.ToString().Split(':');
        return string.Join(":", groups.Take(3)) + "::x";
    }
}
