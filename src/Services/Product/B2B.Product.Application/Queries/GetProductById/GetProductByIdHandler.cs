using B2B.Product.Application.Interfaces;
using B2B.Product.Application.Queries.GetProducts;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Queries.GetProductById;

public sealed class GetProductByIdHandler(
    IReadProductRepository productRepository,   // read replica — NoTracking
    ICacheService cacheService,
    ICurrentUser currentUser)
    : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        // Scope the cache key to the tenant so cross-tenant leakage is impossible
        // even if a global query filter is missing.
        var cacheKey = $"product:{currentUser.TenantId}:{request.ProductId}";

        var cached = await cacheService.GetAsync<ProductDto>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || product.TenantId != currentUser.TenantId)
            return Error.NotFound("Product.NotFound", $"Product {request.ProductId} not found.");

        var dto = new ProductDto(
            product.Id, product.Name, product.Description, product.Sku,
            product.Price.Amount, product.Price.Currency,
            product.StockQuantity, product.IsInStock, product.IsLowStock,
            product.Category?.Name, product.ImageUrl,
            product.Status.ToString(), product.CreatedAt);

        // Only cache successful hits — never cache null so not-found requests
        // always check the DB (expected to be rare; products are usually found).
        await cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);
        return dto;
    }
}
