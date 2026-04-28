using Microsoft.EntityFrameworkCore;
using B2B.Product.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository — uses IDbContextFactory&lt;ProductDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class ProductReadRepository(IDbContextFactory<ProductDbContext> factory)
    : BaseReadRepository<ProductEntity, Guid, ProductDbContext>(factory), IReadProductRepository
{
    public async Task<PagedList<ProductEntity>> GetPagedAsync(
        Guid tenantId, int page, int pageSize,
        string? search = null, Guid? categoryId = null,
        CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);

        var query = ctx.Products
            .Include(p => p.Category)
            .Where(p => p.TenantId == tenantId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                p.Sku.Contains(search) ||
                p.Description.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<ProductEntity>.Create(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ProductEntity>> GetLowStockAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Products
            .Where(p => p.TenantId == tenantId && p.StockQuantity <= p.LowStockThreshold)
            .ToListAsync(ct);
    }
}
