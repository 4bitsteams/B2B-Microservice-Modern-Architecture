using System.Linq.Expressions;

namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Read-only repository abstraction for query handlers.
/// Implementations target a read replica with change tracking disabled,
/// so they must never be used to persist changes.
/// </summary>
public interface IReadRepository<TEntity, TId>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}
