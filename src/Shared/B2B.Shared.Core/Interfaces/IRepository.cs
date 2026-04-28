using System.Linq.Expressions;
using B2B.Shared.Core.Domain;

namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Generic write repository abstraction for aggregate roots.
/// Implementations target the primary database with EF Core change tracking
/// enabled, making them suitable for command handlers that need to persist
/// mutations.
///
/// Never inject this interface into query handlers — use
/// <see cref="IReadRepository{TEntity, TId}"/> instead to ensure reads hit
/// the replica and do not incur tracking overhead.
///
/// All methods that modify state are synchronous (<see cref="Update"/>,
/// <see cref="Remove"/>) because EF Core only sends changes to the database
/// when <c>IUnitOfWork.SaveChangesAsync</c> is called.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type managed by this repository.</typeparam>
/// <typeparam name="TId">The type of the aggregate's identifier.</typeparam>
public interface IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Returns the aggregate with the specified <paramref name="id"/>, or
    /// <see langword="null"/> if it does not exist.
    /// </summary>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>Returns all aggregates. Use with caution on large tables.</summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns all aggregates matching <paramref name="predicate"/>.</summary>
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> when at least one aggregate matches
    /// <paramref name="predicate"/>. More efficient than <see cref="FindAsync"/>
    /// for existence checks.
    /// </summary>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Stages <paramref name="entity"/> for insertion.
    /// The INSERT is sent to the database on the next <c>SaveChangesAsync</c> call.
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Marks <paramref name="entity"/> as modified.
    /// The UPDATE is sent to the database on the next <c>SaveChangesAsync</c> call.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Marks <paramref name="entity"/> for deletion.
    /// The DELETE is sent to the database on the next <c>SaveChangesAsync</c> call.
    /// </summary>
    void Remove(TEntity entity);
}
