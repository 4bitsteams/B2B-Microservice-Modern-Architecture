using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Queries.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? CategoryId = null) : IQuery<PagedList<ProductDto>>;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity,
    bool IsInStock,
    bool IsLowStock,
    string? CategoryName,
    string? ImageUrl,
    string Status,
    DateTime CreatedAt);
