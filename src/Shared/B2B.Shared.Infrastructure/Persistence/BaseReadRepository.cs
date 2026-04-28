using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Persistence;

/// <summary>
/// Base class for read-only repositories.
///
/// Each public method creates its own short-lived DbContext from the factory
/// (replica connection, QueryTrackingBehavior.NoTracking) and disposes it
/// immediately after the query completes. This means:
///   • No change-tracker overhead on reads.
///   • No long-lived connections held between queries.
///   • No accidental writes: SaveChangesAsync is not exposed.
///
/// Concrete implementations that need JOINs or complex queries access
/// the protected <see cref="Factory"/> to create a context inline.
/// </summary>
public abstract class BaseReadRepository<TEntity, TId, TContext>(IDbContextFactory<TContext> factory)
    where TEntity : class
    where TId : notnull
    where TContext : BaseDbContext
{
    // Exposed so concrete subclasses can open a context for custom queries.
    protected readonly IDbContextFactory<TContext> Factory = factory;

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        // FindAsync on a fresh context has an empty change-tracker cache,
        // so it always issues a DB round-trip. NoTracking is set at context level.
        return await ctx.Set<TEntity>().FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Set<TEntity>().Where(predicate).ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Set<TEntity>().AnyAsync(predicate, ct);
    }
}
