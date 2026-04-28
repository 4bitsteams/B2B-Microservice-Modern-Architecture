using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Specifications;

namespace B2B.Shared.Infrastructure.Persistence;

public abstract class BaseRepository<TEntity, TId, TContext>(TContext context)
    : IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
    where TContext : BaseDbContext
{
    protected readonly TContext Context = context;
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await DbSet.ToListAsync(ct);

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.Where(predicate).ToListAsync(ct);

    /// <summary>
    /// Applies a <see cref="ISpecification{TEntity}"/> — includes eager-loading
    /// and ordering hints declared on the spec.
    /// </summary>
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        ISpecification<TEntity> specification, CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet.Where(specification.ToExpression());

        if (specification.Includes is not null)
            query = specification.Includes.Aggregate(query,
                (q, include) => q.Include(include));

        if (specification.Order is { } order)
            query = order.Descending
                ? query.OrderByDescending(order.KeySelector)
                : query.OrderBy(order.KeySelector);

        return await query.ToListAsync(ct);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.AnyAsync(predicate, ct);

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await DbSet.AddAsync(entity, ct);

    public virtual void Update(TEntity entity) => DbSet.Update(entity);

    public virtual void Remove(TEntity entity) => DbSet.Remove(entity);
}
