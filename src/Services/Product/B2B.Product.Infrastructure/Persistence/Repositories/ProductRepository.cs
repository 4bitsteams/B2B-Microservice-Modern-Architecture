using Microsoft.EntityFrameworkCore;
using B2B.Product.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Infrastructure.Persistence.Repositories;

/// <summary>
/// Write repository — uses the scoped ProductDbContext (primary connection, tracking ON).
/// Only contains methods needed by command handlers.
/// </summary>
public sealed class ProductRepository(ProductDbContext context)
    : BaseRepository<ProductEntity, Guid, ProductDbContext>(context), IProductRepository
{
    // Used by CreateProductHandler to check SKU uniqueness on the primary before insert.
    public async Task<ProductEntity?> GetBySkuAsync(string sku, Guid tenantId, CancellationToken ct = default) =>
        await DbSet
            .FirstOrDefaultAsync(p => p.Sku == sku.ToUpperInvariant() && p.TenantId == tenantId, ct);
}
