using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;

namespace Service.Tests;

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
}

internal class FakeRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    protected readonly List<T> Items = new();

    public FakeRepository(params T[] items) => Items.AddRange(items);

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == 0)
        {
            entity.Id = Items.Count == 0 ? 1 : Items.Max(x => x.Id) + 1;
        }

        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
    }

    public void Delete(T entity) => Items.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakePromoCodeRepository : FakeRepository<PromoCode>, IPromoCodeRepository
{
    public FakePromoCodeRepository(params PromoCode[] items) : base(items)
    {
    }

    public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.Code == code));
}

internal sealed class FakeUserRepository : FakeRepository<User>, IUserRepository
{
    public FakeUserRepository(params User[] items) : base(items)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => u.Email == email));
}

internal sealed class FakeCancellationRuleRepository : FakeRepository<CancellationRule>, ICancellationRuleRepository
{
    public FakeCancellationRuleRepository(params CancellationRule[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<CancellationRule>> GetByCabinClassAsync(CabinClass cabinClass, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CancellationRule>>(Items.Where(r => r.CabinClass == cabinClass).ToList());
}
