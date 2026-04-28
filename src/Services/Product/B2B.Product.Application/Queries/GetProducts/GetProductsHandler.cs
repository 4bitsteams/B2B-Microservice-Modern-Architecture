using B2B.Product.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Queries.GetProducts;

public sealed class GetProductsHandler(
    IReadProductRepository productRepository,   // read replica — NoTracking
    ICacheService cacheService,
    ICurrentUser currentUser)
    : IQueryHandler<GetProductsQuery, PagedList<ProductDto>>
{
    public async Task<Result<PagedList<ProductDto>>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"products:tenant:{currentUser.TenantId}:page:{request.Page}:size:{request.PageSize}:search:{request.Search}:cat:{request.CategoryId}";

        // GetOrCreateAsync uses a per-key SemaphoreSlim to prevent stampede: concurrent
        // cache misses on the same key queue behind the first caller rather than all
        // hitting the database simultaneously.
        var result = await cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                var products = await productRepository.GetPagedAsync(
                    currentUser.TenantId, request.Page, request.PageSize,
                    request.Search, request.CategoryId, cancellationToken);

                return products.Map(p => new ProductDto(
                    p.Id, p.Name, p.Description, p.Sku,
                    p.Price.Amount, p.Price.Currency,
                    p.StockQuantity, p.IsInStock, p.IsLowStock,
                    p.Category?.Name, p.ImageUrl,
                    p.Status.ToString(), p.CreatedAt));
            },
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return result;
    }
}
