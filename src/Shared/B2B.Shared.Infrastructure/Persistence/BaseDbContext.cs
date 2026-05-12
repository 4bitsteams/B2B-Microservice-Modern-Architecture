using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Persistence;

/// <summary>
/// Base EF Core DbContext shared by all microservice contexts.
///
/// Responsibilities:
///   • Auditable properties (CreatedAt / UpdatedAt) — set automatically on SaveChanges.
///   • Unique constraint translation — catches Npgsql 23505 and throws
///     <see cref="UniqueConstraintException"/> so Application handlers can return
///     <c>Error.Conflict</c> without referencing EF Core directly.
///   • Optimistic concurrency — PostgreSQL <c>xmin</c> system column is registered
///     as a concurrency token on every <see cref="AggregateRoot{TId}"/> entity.
///     EF Core includes the current xmin value in UPDATE/DELETE WHERE clauses;
///     if another transaction modified the row in the interim, xmin will have
///     changed and the UPDATE will match zero rows, triggering
///     <see cref="DbUpdateConcurrencyException"/> → <see cref="ConcurrencyException"/>.
///     No extra column is required — PostgreSQL manages xmin automatically.
///   • Global tenant query filter — entities implementing <see cref="ITenantEntity"/>
///     are scoped to the current user's TenantId at the ORM level.
///   • Transaction helpers — <see cref="ExecuteInTransactionAsync"/> and
///     <see cref="ExecuteInTransactionAsync{TResult}"/> for explicit transaction
///     control, required when using pessimistic row locks
///     (<see cref="ILockableRepository{TEntity,TId}"/>).
///
/// CANCELLATION TOKEN SAFETY
/// ─────────────────────────
/// All async methods accept a <see cref="CancellationToken"/> and propagate it
/// to every EF Core operation. The pattern:
///   1. <c>ct.ThrowIfCancellationRequested()</c> at method entry guards against
///      already-cancelled tokens before expensive I/O begins.
///   2. The token is passed to <c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>,
///      <c>CommitAsync</c>, and <c>RollbackAsync</c> so shutdowns are always clean.
///   3. On cancellation, EF Core/Npgsql cancels the in-flight PostgreSQL query;
///      the connection is returned to the pool without a round-trip.
///
/// Global tenant filter notes:
///   • The filter uses a lazy service-locator pattern (<c>_serviceProvider</c>) to
///     resolve <see cref="ICurrentUser"/> at query time rather than at context
///     construction time. This is required because DbContext is scoped but filter
///     expressions are compiled once per model.
///   • Migrations always bypass the filter (EF Core disables global filters during
///     <c>MigrateAsync</c> and design-time scaffolding automatically).
///   • Background services / sagas running outside an HTTP context will have
///     <c>ICurrentUser.TenantId == Guid.Empty</c>; the filter allows an empty Guid
///     so those paths are not broken (tenant scoping for background jobs must be
///     applied explicitly in their query logic).
/// </summary>
public abstract class BaseDbContext : DbContext, IUnitOfWork
{
    private readonly IServiceProvider? _serviceProvider;

    protected BaseDbContext(DbContextOptions options) : base(options) { }

    protected BaseDbContext(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options)
    {
        _serviceProvider = serviceProvider;
    }

    // Resolved lazily per query so we always get the *current* request's TenantId.
    private Guid CurrentTenantId =>
        _serviceProvider?.GetService<ICurrentUser>()?.TenantId ?? Guid.Empty;

    // ── IUnitOfWork.SaveChangesAsync ─────────────────────────────────────────

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Guard against already-cancelled tokens before touching the DB.
        cancellationToken.ThrowIfCancellationRequested();

        SetAuditableProperties();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Optimistic concurrency conflict: another transaction modified (xmin
            // changed) or deleted the row between our read and our write.
            // The caller should return Error.Conflict and let the client retry.
            throw new ConcurrencyException(ex.Message, ex);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Unique constraint violation (Npgsql 23505).
            // Application handlers return Error.Conflict without referencing EF Core.
            throw new UniqueConstraintException(ex.InnerException?.Message ?? ex.Message, ex);
        }
    }

    // ── IUnitOfWork.ExecuteInTransactionAsync (void) ────────────────────────

    /// <summary>
    /// Wraps <paramref name="action"/> in a database transaction and calls
    /// <c>SaveChangesAsync</c> before committing.
    ///
    /// Required for pessimistic row locks: <see cref="ILockableRepository{T,TId}"/>
    /// methods acquire locks that remain held only until the transaction commits
    /// or rolls back. Always use this overload or
    /// <see cref="ExecuteInTransactionAsync{TResult}"/> around any
    /// <c>GetByIdForUpdateAsync</c> / <c>GetByIdForShareAsync</c> calls.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Roll back on any exception, then re-throw so the caller can
            // handle the failure (e.g. return Error.Conflict).
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // ── IUnitOfWork.ExecuteInTransactionAsync<TResult> ──────────────────────

    /// <summary>
    /// Wraps <paramref name="action"/> in a database transaction, calls
    /// <c>SaveChangesAsync</c> before committing, and returns
    /// <typeparamref name="TResult"/>.
    ///
    /// Typical pattern with a pessimistic lock:
    /// <code>
    /// var order = await unitOfWork.ExecuteInTransactionAsync(async ct =>
    /// {
    ///     var o = await repo.GetByIdForUpdateAsync(id, ct); // acquires FOR UPDATE lock
    ///     o?.Confirm();                                     // mutate under lock
    ///     return o;                                         // persisted + committed below
    /// }, cancellationToken);
    /// </code>
    /// </summary>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // ── Model configuration ──────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // ── Global tenant query filter ────────────────────────────────────
            // Applied to every entity that implements ITenantEntity.
            // The closure captures `this` so CurrentTenantId is evaluated per-query.
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var tenantMethod = typeof(BaseDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                tenantMethod.Invoke(this, [modelBuilder]);
            }

            // ── Optimistic concurrency via PostgreSQL xmin ────────────────────
            // Applied to every AggregateRoot<TId>-derived entity.
            //
            // xmin is a PostgreSQL system column that stores the transaction ID of
            // the last INSERT or UPDATE. It changes on every write, making it a
            // perfect concurrency token without any extra columns.
            //
            // EF Core (Npgsql) automatically:
            //   1. Reads xmin when an entity is loaded.
            //   2. Includes "WHERE xmin = @original_xmin" in UPDATE/DELETE statements.
            //   3. Throws DbUpdateConcurrencyException when the WHERE matches 0 rows.
            //
            // BaseDbContext.SaveChangesAsync translates this to ConcurrencyException.
            if (IsAggregateRoot(entityType.ClrType))
            {
                modelBuilder
                    .Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .HasColumnType("xid")
                    .HasColumnName("xmin")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetAuditableProperties()
    {
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            // Only set UpdatedAt on explicit updates — not on initial creation.
            // UpdatedAt == null means the record was never modified after creation.
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || CurrentTenantId == Guid.Empty);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? string.Empty;
        return inner.Contains("23505", StringComparison.Ordinal)
            || inner.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("UNIQUE constraint", StringComparison.Ordinal)
            || inner.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is, or inherits
    /// from, the open generic <c>AggregateRoot&lt;TId&gt;</c>.
    /// Walks the full inheritance chain to handle multi-level hierarchies.
    /// </summary>
    private static bool IsAggregateRoot(Type type)
    {
        var current = type;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return true;

            current = current.BaseType;
        }
        return false;
    }
}
