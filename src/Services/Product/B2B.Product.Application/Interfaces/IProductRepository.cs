using B2B.Product.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ProductEntity = B2B.Product.Domain.Entities.Product;

namespace B2B.Product.Application.Interfaces;

/// <summary>
/// Write repository for <see cref="ProductEntity"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only.
/// </summary>
public interface IProductRepository : IRepository<ProductEntity, Guid>
{
    /// <summary>
    /// Returns the product with the given <paramref name="sku"/> within
    /// <paramref name="tenantId"/>, or <see langword="null"/> if not found.
    /// Used by command handlers to enforce SKU uniqueness before insert.
    /// Must run against the primary to avoid stale-read false negatives.
    /// </summary>
    Task<ProductEntity?> GetBySkuAsync(string sku, Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Read-only product repository — targets the read replica with NoTracking.
/// Inject into query handlers only; never call SaveChanges on derived contexts.
/// </summary>
public interface IReadProductRepository : IReadRepository<ProductEntity, Guid>
{
    /// <summary>
    /// Returns a paged, optionally filtered list of products for <paramref name="tenantId"/>.
    /// Applies <paramref name="search"/> as a case-insensitive name/SKU filter when provided,
    /// and restricts to <paramref name="categoryId"/> when specified.
    /// </summary>
    Task<PagedList<ProductEntity>> GetPagedAsync(Guid tenantId, int page, int pageSize,
        string? search = null, Guid? categoryId = null, CancellationToken ct = default);

    /// <summary>
    /// Returns all products for <paramref name="tenantId"/> whose stock quantity
    /// is at or below the low-stock threshold defined on the product.
    /// Used by the low-stock dashboard and reorder alerts.
    /// </summary>
    Task<IReadOnlyList<ProductEntity>> GetLowStockAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Write repository for <see cref="Category"/> aggregates.
/// Targets the primary database with change tracking enabled.
/// Inject into command handlers only.
/// </summary>
public interface ICategoryRepository : IRepository<Category, Guid>
{
    /// <summary>
    /// Returns the category with the given URL <paramref name="slug"/> within
    /// <paramref name="tenantId"/>, or <see langword="null"/> if not found.
    /// Used to enforce slug uniqueness before insert.
    /// </summary>
    Task<Category?> GetBySlugAsync(string slug, Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns all categories belonging to <paramref name="tenantId"/>, ordered by sort order.</summary>
    Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Read-only category repository — targets the read replica with NoTracking.
/// Inject into query handlers only.
/// </summary>
public interface IReadCategoryRepository : IReadRepository<Category, Guid>
{
    /// <summary>Returns all categories belonging to <paramref name="tenantId"/>, ordered by sort order.</summary>
    Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
