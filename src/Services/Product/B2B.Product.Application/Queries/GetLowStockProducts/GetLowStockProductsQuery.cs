using B2B.Product.Application.Queries.GetProducts;
using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Queries.GetLowStockProducts;

public sealed record GetLowStockProductsQuery : IQuery<IReadOnlyList<ProductDto>>;
