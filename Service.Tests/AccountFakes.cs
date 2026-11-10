using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>The e-mail options of accounts. Nobody has saved any unless a test adds a row or lists an address that turned campaigns off.</summary>
internal sealed class FakeNotificationPreferenceRepository : FakeRepository<NotificationPreference>, INotificationPreferenceRepository
{
    /// <summary>Addresses of accounts that turned campaign e-mails off.</summary>
    public HashSet<string> MarketingOptedOutEmails { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<NotificationPreference?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.UserId == userId));

    public Task<IReadOnlySet<string>> GetEmailsWithoutMarketingAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(MarketingOptedOutEmails.Select(e => e.ToLowerInvariant()).ToHashSet());
}

/// <summary>The sign-in sessions of accounts, kept in memory.</summary>
internal sealed class FakeUserSessionRepository : FakeRepository<UserSession>, IUserSessionRepository
{
    public Task<IReadOnlyList<UserSession>> GetOpenAsync(int userId, DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserSession>>(Items.Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now).OrderByDescending(s => s.CreatedAt).ToList());

    public Task<UserSession?> GetByKeyAsync(string sessionKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.SessionKey == sessionKey));

    public Task<bool> IsOpenAsync(int userId, string sessionKey, DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(s => s.UserId == userId && s.SessionKey == sessionKey && s.RevokedAt == null && s.ExpiresAt > now));

    public Task<DateTime?> GetLatestSignInAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).Select(s => (DateTime?)s.CreatedAt).FirstOrDefault());

    public Task<int> DeleteOldAsync(DateTime before, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.RemoveAll(s => s.ExpiresAt < before || (s.RevokedAt != null && s.RevokedAt < before)));
}

/// <summary>Makes the token of a sign-in the way the real session service does, without a database: the session is not stored, every token counts as open.</summary>
internal sealed class FakeAccountSessionService : IAccountSessionService
{
    private readonly ITokenService _tokens;

    public FakeAccountSessionService(ITokenService tokens) => _tokens = tokens;

    public Task<(string AccessToken, DateTime ExpiresAt)> IssueAsync(User user, CancellationToken cancellationToken = default)
        => Task.FromResult(_tokens.CreateToken(user, Guid.NewGuid().ToString("N")));

    public Task<bool> IsOpenAsync(int userId, string sessionKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
