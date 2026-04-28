using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Interfaces;

// ── Write repositories (primary DB, change-tracking) ──────────────────────────

/// <summary>
/// Write repository for <see cref="User"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only.
/// </summary>
public interface IUserRepository : IRepository<User, Guid>
{
    /// <summary>
    /// Returns the user with the given <paramref name="email"/> within
    /// <paramref name="tenantId"/>, or <see langword="null"/> if not found.
    /// Used by login to look up a user before verifying their password.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the user with the given <paramref name="email"/> including their
    /// <see cref="User.UserRoles"/> navigation, or <see langword="null"/> if not found.
    /// Cross-tenant lookup — no tenant filter — used for token refresh where the
    /// tenant is resolved from the JWT claim.
    /// </summary>
    Task<User?> GetByEmailWithRolesAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Returns the user with the given <paramref name="id"/> including their
    /// <see cref="User.RefreshTokens"/> collection, or <see langword="null"/> if not found.
    /// Used by the refresh-token flow to locate and revoke the presented token.
    /// </summary>
    Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Write repository for <see cref="Tenant"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only.
/// </summary>
public interface ITenantRepository : IRepository<Tenant, Guid>
{
    /// <summary>
    /// Returns the tenant with the given URL <paramref name="slug"/>,
    /// or <see langword="null"/> if not found.
    /// </summary>
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> when a tenant with the given
    /// <paramref name="slug"/> already exists. Used during tenant registration
    /// to enforce slug uniqueness before insert.
    /// </summary>
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// Write repository for <see cref="Role"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only.
/// </summary>
public interface IRoleRepository : IRepository<Role, Guid>
{
    /// <summary>Returns the role with the given <paramref name="name"/>, or <see langword="null"/>.</summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Returns all roles assigned to the user identified by <paramref name="userId"/>.</summary>
    Task<IReadOnlyList<Role>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}

// ── Read repositories (read replica, NoTracking, inject into query handlers) ───

/// <summary>Read-only user queries — targets the read replica with NoTracking.
/// Never call SaveChanges on derived contexts.</summary>
public interface IReadUserRepository : IReadRepository<User, Guid>
{
    /// <summary>Returns a paged list of users belonging to <paramref name="tenantId"/>.</summary>
    Task<PagedList<User>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns the user with the given <paramref name="userId"/> including their
    /// role assignments, or <see langword="null"/> if not found.
    /// </summary>
    Task<User?> GetWithRolesAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Read-only tenant queries — targets the read replica with NoTracking.
/// Never call SaveChanges on derived contexts.</summary>
public interface IReadTenantRepository : IReadRepository<Tenant, Guid>
{
    /// <summary>Returns the tenant with the given URL <paramref name="slug"/>, or <see langword="null"/>.</summary>
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
}

/// <summary>Read-only role queries — targets the read replica with NoTracking.
/// Never call SaveChanges on derived contexts.</summary>
public interface IReadRoleRepository : IReadRepository<Role, Guid>
{
    /// <summary>Returns all roles in the system, ordered by name.</summary>
    Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default);
}
