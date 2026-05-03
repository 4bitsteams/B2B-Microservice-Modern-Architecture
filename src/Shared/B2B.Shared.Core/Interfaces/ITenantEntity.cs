namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Marks a domain entity as tenant-scoped.
///
/// <see cref="BaseDbContext"/> uses this interface to apply a global EF Core
/// query filter that automatically restricts all reads to the current tenant,
/// so handlers never need to add <c>.Where(e => e.TenantId == currentUser.TenantId)</c>
/// manually. Write operations still require the TenantId to be set on the entity
/// at creation time via the aggregate factory method.
///
/// Multi-tenancy guarantee:
///   All entities that implement this interface are automatically filtered.
///   Any entity that stores tenant data and does NOT implement this interface
///   must be explicitly scoped in its repository query.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
