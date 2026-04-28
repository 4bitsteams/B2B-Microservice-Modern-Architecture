using B2B.Product.Application.Queries.GetProducts;
using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDto>;
