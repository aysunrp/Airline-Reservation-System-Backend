using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface IEmailTokenRepository : IBaseRepository<EmailToken>
{
    /// <summary>The token with this hash and purpose, with its user (tracked); null when there is none.</summary>
    Task<EmailToken?> GetByHashAsync(string tokenHash, EmailTokenPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>Tokens of the user and purpose that were not used yet (tracked), so they can be closed.</summary>
    Task<IReadOnlyList<EmailToken>> GetUnusedAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>When the user's latest token of this purpose was made; null when there is none.</summary>
    Task<DateTime?> GetLatestCreatedAtAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>How many tokens of this purpose were made for the user since the given moment (used or not).</summary>
    Task<int> CountCreatedSinceAsync(int userId, EmailTokenPurpose purpose, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Removes tokens that expired before the given moment; returns how many.</summary>
    Task<int> DeleteExpiredAsync(DateTime before, CancellationToken cancellationToken = default);
}
