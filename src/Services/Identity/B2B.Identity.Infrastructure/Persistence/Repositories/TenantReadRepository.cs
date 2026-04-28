using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only tenant repository — uses IDbContextFactory&lt;IdentityDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class TenantReadRepository(IDbContextFactory<IdentityDbContext> factory)
    : BaseReadRepository<Tenant, Guid, IdentityDbContext>(factory), IReadTenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant(), ct);
    }
}
