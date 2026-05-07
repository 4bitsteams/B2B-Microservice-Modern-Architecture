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
///   • Global tenant query filter — entities that implement <see cref="ITenantEntity"/>
///     are automatically scoped to the current user's TenantId. This enforces
///     row-level multi-tenant isolation at the ORM level, removing the need for
///     manual <c>.Where(e => e.TenantId == currentUser.TenantId)</c> in every handler.
///   • Transaction helper — <see cref="ExecuteInTransactionAsync"/> for use-cases
///     that need explicit transaction control.
///
/// Global tenant filter notes:
///   • The filter uses a lazy service-locator pattern (<c>_serviceProvider</c>) to
///     resolve <see cref="ICurrentUser"/> at query time rather than at context
///     construction time. This is required because DbContext is scoped but filter
///     expressions are compiled once per model.
///   • Migrations always bypass the filter (EF Core disables global filters during
///     <c>MigrateAsync</c> and design-time scaffolding automatically).
///   • Background services / sagas that run outside an HTTP context will have
///     <c>ICurrentUser.TenantId == Guid.Empty</c>; the filter allows empty Guid
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditableProperties();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate to a domain exception so Application handlers can return
            // Error.Conflict without referencing EF Core directly.
            // Triggered when an optimistic-concurrency token (e.g. PostgreSQL xmin,
            // or a RowVersion column) detects that another writer modified the row
            // between our read and our write.
            throw new ConcurrencyException(ex.Message, ex);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Translate to a domain exception so Application handlers can return
            // Error.Conflict without referencing EF Core or Npgsql directly.
            throw new UniqueConstraintException(ex.InnerException?.Message ?? ex.Message, ex);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? string.Empty;
        // PostgreSQL unique violation SqlState = 23505; also matches SQLite/SQL Server phrasing
        return inner.Contains("23505", StringComparison.Ordinal)
            || inner.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("UNIQUE constraint", StringComparison.Ordinal)
            || inner.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private void SetAuditableProperties()
    {
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            // Only set UpdatedAt on explicit updates — not on initial creation.
            // This preserves a meaningful distinction: UpdatedAt == null means the
            // record was never modified after creation.
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Global tenant query filter ────────────────────────────────────────────
        // Applied to every entity type that implements ITenantEntity.
        // The closure captures `this` so CurrentTenantId is evaluated per-query
        // against the live ICurrentUser, not baked in at startup.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var method = typeof(BaseDbContext)
                .GetMethod(nameof(ApplyTenantFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || CurrentTenantId == Guid.Empty);
    }
}
