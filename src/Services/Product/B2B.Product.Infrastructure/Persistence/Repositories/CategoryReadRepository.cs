using Microsoft.EntityFrameworkCore;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Product.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only category repository — uses IDbContextFactory&lt;ProductDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class CategoryReadRepository(IDbContextFactory<ProductDbContext> factory)
    : BaseReadRepository<Category, Guid, ProductDbContext>(factory), IReadCategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Set<Category>()
            .Include(c => c.SubCategories)
            .Where(c => c.TenantId == tenantId && c.ParentCategoryId == null)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }
}
