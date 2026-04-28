using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only user repository — uses IDbContextFactory&lt;IdentityDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class UserReadRepository(IDbContextFactory<IdentityDbContext> factory)
    : BaseReadRepository<User, Guid, IdentityDbContext>(factory), IReadUserRepository
{
    public async Task<PagedList<User>> GetPagedByTenantAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);

        var query = ctx.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<User>.Create(items, page, pageSize, total);
    }

    public async Task<User?> GetWithRolesAsync(Guid userId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }
}
