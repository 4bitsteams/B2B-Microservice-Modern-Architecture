using B2B.Product.Application.Interfaces;
using B2B.Product.Application.Queries.GetProducts;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Application.Queries.GetLowStockProducts;

public sealed class GetLowStockProductsHandler(
    IReadProductRepository productRepository,  // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetLowStockProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(
        GetLowStockProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await productRepository.GetLowStockAsync(
            currentUser.TenantId, cancellationToken);

        var dtos = products.Select(p => new ProductDto(
            p.Id, p.Name, p.Description, p.Sku,
            p.Price.Amount, p.Price.Currency,
            p.StockQuantity, p.IsInStock, p.IsLowStock,
            p.Category?.Name, p.ImageUrl,
            p.Status.ToString(), p.CreatedAt)).ToList();

        return dtos;
    }
}
