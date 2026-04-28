using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only role repository — uses IDbContextFactory&lt;IdentityDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class RoleReadRepository(IDbContextFactory<IdentityDbContext> factory)
    : BaseReadRepository<Role, Guid, IdentityDbContext>(factory), IReadRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Roles
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }
}
