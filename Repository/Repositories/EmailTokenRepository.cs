using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class EmailTokenRepository : BaseRepository<EmailToken>, IEmailTokenRepository
{
    public EmailTokenRepository(AppDbContext context) : base(context)
    {
    }

    public Task<EmailToken?> GetByHashAsync(string tokenHash, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
        => Set.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose, cancellationToken);

    public async Task<IReadOnlyList<EmailToken>> GetUnusedAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null).ToListAsync(cancellationToken);

    public async Task<DateTime?> GetLatestCreatedAtAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.UserId == userId && t.Purpose == purpose)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountCreatedSinceAsync(int userId, EmailTokenPurpose purpose, DateTime since, CancellationToken cancellationToken = default)
        => Set.CountAsync(t => t.UserId == userId && t.Purpose == purpose && t.CreatedAt >= since, cancellationToken);

    public async Task<IReadOnlyList<EmailToken>> GetUnusedForBookingAsync(int bookingId, EmailTokenPurpose purpose = EmailTokenPurpose.BookingConfirm, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.BookingId == bookingId && t.Purpose == purpose && t.UsedAt == null).ToListAsync(cancellationToken);

    public async Task<DateTime?> GetLatestCreatedAtForBookingAsync(int bookingId, EmailTokenPurpose purpose = EmailTokenPurpose.BookingConfirm, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.BookingId == bookingId && t.Purpose == purpose)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTime before, CancellationToken cancellationToken = default)
        => Set.Where(t => t.ExpiresAt < before).ExecuteDeleteAsync(cancellationToken);
}
