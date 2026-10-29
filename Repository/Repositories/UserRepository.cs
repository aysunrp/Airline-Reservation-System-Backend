using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Set.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> SearchAsync(string? search, UserRole? role, bool? isActive, int take, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(u => u.Email.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }

        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }

        if (isActive is not null)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        return await query.OrderByDescending(u => u.Id).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveInRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        => Set.CountAsync(u => u.Role == role && u.IsActive, cancellationToken);

    public async Task<IReadOnlyList<int>> GetActiveStaffIdsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)).Select(u => u.Id).ToListAsync(cancellationToken);

    public Task<bool> HasBookingsAsync(int userId, CancellationToken cancellationToken = default)
        => Context.Bookings.AnyAsync(b => b.UserId == userId, cancellationToken);

    public Task<UserState?> GetStateAsync(int id, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserState(u.Role, u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
