using Domain.Common;

namespace Repository.Repositories.Interfaces;

/// <summary>
/// Generic data access. Methods never call SaveChanges implicitly; the caller (Service layer)
/// decides when a unit of work is committed by calling <see cref="SaveChangesAsync"/> once.
/// </summary>
public interface IBaseRepository<T> where T : BaseEntity
{
    /// <summary>Returns all rows without change tracking.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a tracked entity, or null when it does not exist.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Marks the entity as modified (attaches it if it is not tracked) and stamps UpdatedAt.</summary>
    void Update(T entity);

    void Delete(T entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
