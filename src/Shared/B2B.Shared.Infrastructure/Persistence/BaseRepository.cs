using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Specifications;

namespace B2B.Shared.Infrastructure.Persistence;

/// <summary>
/// Generic write repository backed by a <see cref="BaseDbContext"/>-derived context.
///
/// Implements both <see cref="IRepository{TEntity,TId}"/> (standard CRUD) and
/// <see cref="ILockableRepository{TEntity,TId}"/> (pessimistic row locking).
///
/// CANCELLATION TOKEN SAFETY
/// ─────────────────────────
/// Every async method:
///   1. Calls <c>ct.ThrowIfCancellationRequested()</c> before issuing any I/O so
///      already-cancelled tokens are detected instantly without a DB round-trip.
///   2. Passes <paramref name="ct"/> to all EF Core operations so in-flight
///      PostgreSQL queries are cancelled via the Npgsql cancellation API.
///
/// PESSIMISTIC LOCKING (ILockableRepository)
/// ──────────────────────────────────────────
/// <see cref="GetByIdForUpdateAsync"/> and <see cref="GetByIdForShareAsync"/> issue
/// <c>SELECT … FOR UPDATE</c> / <c>SELECT … FOR SHARE</c> raw SQL to acquire a
/// PostgreSQL row-level lock, then load the entity with change tracking so EF Core
/// can later persist mutations in the same transaction.
///
/// These methods MUST be called inside an explicit database transaction started via
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>; otherwise the lock
/// is released immediately at statement end and provides no protection.
///
/// OPTIMISTIC CONCURRENCY
/// ──────────────────────
/// <see cref="BaseDbContext"/> configures the PostgreSQL <c>xmin</c> system column
/// as a shadow concurrency token on all <see cref="AggregateRoot{TId}"/> entities.
/// EF Core automatically includes <c>WHERE xmin = @original</c> in UPDATE/DELETE
/// statements; if another transaction wrote to the row, the UPDATE matches zero rows
/// and <see cref="Core.Common.ConcurrencyException"/> is thrown (translated from
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>).
///
/// Register as <c>Scoped</c> — lifetime must match the owning <typeparamref name="TContext"/>.
/// </summary>
public abstract class BaseRepository<TEntity, TId, TContext>(TContext context)
    : ILockableRepository<TEntity, TId>, IStreamingRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
    where TContext : BaseDbContext
{
    /// <summary>The owning DbContext instance (request-scoped).</summary>
    protected readonly TContext Context = context;

    /// <summary>Typed DbSet for <typeparamref name="TEntity"/> — convenience shortcut for subclasses.</summary>
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    // ── IRepository<TEntity, TId> ────────────────────────────────────────────

    /// <inheritdoc/>
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await DbSet.FindAsync([id], ct);
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await DbSet.ToListAsync(ct);
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await DbSet.Where(predicate).ToListAsync(ct);
    }

    /// <summary>
    /// Applies a <see cref="ISpecification{TEntity}"/> — includes eager-loading
    /// and ordering hints declared on the spec.
    /// </summary>
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        ISpecification<TEntity> specification, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

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
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await DbSet.AnyAsync(predicate, ct);
    }

    /// <inheritdoc/>
    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await DbSet.AddAsync(entity, ct);
    }

    /// <inheritdoc/>
    public virtual void Update(TEntity entity) => DbSet.Update(entity);

    /// <inheritdoc/>
    public virtual void Remove(TEntity entity) => DbSet.Remove(entity);

    // ── ILockableRepository<TEntity, TId> ────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Implementation detail: issues a raw <c>SELECT 1 … FOR UPDATE</c> to
    /// PostgreSQL to acquire an exclusive row lock, then calls
    /// <see cref="DbSet"/>.FindAsync so EF Core loads the entity with full change
    /// tracking. The two-step approach works correctly with owned types, shadow
    /// properties (including the <c>xmin</c> concurrency token), and navigation
    /// properties that would complicate a <c>FromSqlInterpolated</c> query.
    ///
    /// If the row does not exist the lock statement matches zero rows (a no-op)
    /// and <see langword="null"/> is returned — no exception is thrown.
    ///
    /// If another transaction already holds a conflicting lock the call will block
    /// until that transaction commits or rolls back, or until
    /// <paramref name="ct"/> is cancelled.
    /// </remarks>
    public virtual async Task<TEntity?> GetByIdForUpdateAsync(
        TId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await AcquireRowLockAsync(id, forUpdate: true, ct);
        return await DbSet.FindAsync([id], ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Issues <c>SELECT 1 … FOR SHARE</c>. Multiple callers can hold shared locks
    /// concurrently; only writers are blocked. Use when you need a stable read that
    /// prevents updates, but you do not intend to modify the row yourself.
    /// </remarks>
    public virtual async Task<TEntity?> GetByIdForShareAsync(
        TId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await AcquireRowLockAsync(id, forUpdate: false, ct);
        return await DbSet.FindAsync([id], ct);
    }

    // ── IStreamingRepository<TEntity, TId> ───────────────────────────────────

    /// <summary>
    /// Streams entities matching <paramref name="predicate"/> one at a time using
    /// EF Core's <c>AsAsyncEnumerable()</c> server-side cursor.
    ///
    /// Unlike <see cref="FindAsync(Expression{Func{TEntity,bool}},CancellationToken)"/>,
    /// this method never buffers the full result set — memory consumption is bounded
    /// to the Npgsql fetch window, not the total row count. Use for large collections
    /// such as nightly batch jobs, report exports, or bulk reprocessing.
    ///
    /// Entities are loaded with change tracking so callers can mutate and persist
    /// within the same unit of work scope.
    /// </summary>
    public virtual async IAsyncEnumerable<TEntity> StreamAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var query = predicate is null
            ? DbSet.AsQueryable()
            : DbSet.Where(predicate);

        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(ct))
            yield return entity;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Issues the raw SQL row-lock statement.
    /// Table and column names come from EF Core model metadata (not user input),
    /// so they are safe to embed directly in the SQL string. The <c>id</c> value
    /// is always passed as a parameterised argument via
    /// <see cref="RelationalDatabaseFacadeExtensions.ExecuteSqlAsync"/>.
    /// </summary>
    private async Task AcquireRowLockAsync(TId id, bool forUpdate, CancellationToken ct)
    {
        var lockMode = forUpdate ? "FOR UPDATE" : "FOR SHARE";

        var entityType = Context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' was not found in the EF Core model.");

        var tableName = entityType.GetSchemaQualifiedTableName()
            ?? entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"Could not resolve table name for '{typeof(TEntity).Name}'.");

        var pk = entityType.FindPrimaryKey()?.Properties;
        var pkColumnName = (pk is { Count: > 0 } ? pk[0].GetColumnName() : null)
            ?? throw new InvalidOperationException(
                $"Could not resolve primary key column for '{typeof(TEntity).Name}'.");

        // Using ExecuteSqlAsync (FormattableString overload):
        //   • Table and column names are embedded as string literals — safe because
        //     they originate from EF Core model metadata, never from user input.
        //   • {id} becomes a parameterised argument ($1 in Npgsql), preventing injection.
        await Context.Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM "{tableName}" WHERE "{pkColumnName}" = {id} {lockMode}""",
            ct);
    }
}
