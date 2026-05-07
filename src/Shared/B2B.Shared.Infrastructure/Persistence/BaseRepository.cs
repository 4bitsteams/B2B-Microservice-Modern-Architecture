using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Specifications;

namespace B2B.Shared.Infrastructure.Persistence;

/// <summary>
/// Generic write repository backed by a <see cref="BaseDbContext"/>-derived context.
///
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item><description>Provides standard CRUD operations — <c>GetByIdAsync</c>, <c>AddAsync</c>, <c>Update</c>, <c>Remove</c>.</description></item>
///   <item><description>Supports predicate-based and <see cref="ISpecification{TEntity}"/>-based queries with optional eager-loading and ordering.</description></item>
///   <item><description>Change tracking is enabled so modifications are flushed on the next <c>IUnitOfWork.SaveChangesAsync</c> call.</description></item>
/// </list>
/// </para>
///
/// <para>
/// This class is open for extension: service-specific repositories inherit from it
/// and override or extend methods for custom queries (e.g. <c>GetBySkuAsync</c>).
/// </para>
///
/// <para>
/// Register as <c>Scoped</c> — lifetime must match the owning <typeparamref name="TContext"/>.
/// </para>
/// </summary>
/// <typeparam name="TEntity">Aggregate root type managed by this repository.</typeparam>
/// <typeparam name="TId">Identity type of <typeparamref name="TEntity"/> (e.g. <see cref="Guid"/>).</typeparam>
/// <typeparam name="TContext">The EF Core DbContext implementation for this service.</typeparam>
public abstract class BaseRepository<TEntity, TId, TContext>(TContext context)
    : IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
    where TContext : BaseDbContext
{
    /// <summary>The owning DbContext instance (request-scoped).</summary>
    protected readonly TContext Context = context;

    /// <summary>Typed DbSet for <typeparamref name="TEntity"/> — convenience shortcut for subclasses.</summary>
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    /// <inheritdoc/>
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await DbSet.FindAsync([id], ct);

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await DbSet.ToListAsync(ct);

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.Where(predicate).ToListAsync(ct);

    /// <summary>
    /// Applies a <see cref="ISpecification{TEntity}"/> — includes eager-loading
    /// and ordering hints declared on the spec.
    /// </summary>
    /// <param name="specification">
    /// Specification encapsulating the filter predicate, optional <c>Include</c> paths,
    /// and an optional order expression.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        ISpecification<TEntity> specification, CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet.Where(specification.ToExpression());

        if (specification.Includes is not null)
        {
            query = specification.Includes.Aggregate(query,
                (q, include) => q.Include(include));
        }

        if (specification.Order is { } order)
        {
            query = order.Descending
                ? query.OrderByDescending(order.KeySelector)
                : query.OrderBy(order.KeySelector);
        }

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.AnyAsync(predicate, ct);

    /// <inheritdoc/>
    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await DbSet.AddAsync(entity, ct);

    /// <inheritdoc/>
    public virtual void Update(TEntity entity) => DbSet.Update(entity);

    /// <inheritdoc/>
    public virtual void Remove(TEntity entity) => DbSet.Remove(entity);
}
