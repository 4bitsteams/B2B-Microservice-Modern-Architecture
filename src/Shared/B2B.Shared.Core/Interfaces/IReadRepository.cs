using System.Linq.Expressions;

namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Read-only repository abstraction for query handlers.
/// Implementations target a read replica with change tracking disabled,
/// so they must never be used to persist changes.
///
/// <see cref="StreamAsync"/> streams large result sets one row at a time via a
/// server-side cursor to avoid buffering the full collection in memory.
/// </summary>
public interface IReadRepository<TEntity, TId>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Streams entities matching <paramref name="predicate"/> one at a time from the
    /// read replica using a server-side cursor. Memory consumption is bounded to the
    /// Npgsql fetch window regardless of total row count.
    ///
    /// Use for large exports, batch jobs, and report queries where buffering the full
    /// result set would cause a GC-pressure spike.
    /// </summary>
    IAsyncEnumerable<TEntity> StreamAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);
}
