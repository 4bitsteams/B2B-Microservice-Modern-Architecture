namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Exposes the identity and authorization context of the authenticated caller
/// for the current HTTP request.
///
/// The Infrastructure implementation (<c>CurrentUserService</c>) resolves all
/// properties from JWT claims via <c>IHttpContextAccessor</c>. Inject this
/// interface into handlers and domain services that need to scope data to the
/// current tenant or verify permissions.
///
/// Multi-tenancy: every query or command that touches tenant-owned data must
/// filter by <see cref="TenantId"/> to enforce row-level isolation:
/// <code>
/// .Where(o => o.TenantId == currentUser.TenantId)
/// </code>
/// </summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's unique identifier, parsed from the JWT <c>sub</c> claim.</summary>
    Guid UserId { get; }

    /// <summary>The authenticated user's email address.</summary>
    string Email { get; }

    /// <summary>The tenant the user belongs to, parsed from the JWT <c>tenant_id</c> claim.</summary>
    Guid TenantId { get; }

    /// <summary>The tenant's URL slug (e.g. <c>"acme"</c>), used for multi-tenant routing.</summary>
    string TenantSlug { get; }

    /// <summary>The set of role names assigned to the user, e.g. <c>["Admin", "User"]</c>.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary><see langword="true"/> when the request carries a valid, non-expired JWT.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Returns <see langword="true"/> when the user holds the specified <paramref name="role"/>.</summary>
    bool IsInRole(string role);
}
