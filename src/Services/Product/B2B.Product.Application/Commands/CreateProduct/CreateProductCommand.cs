using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId,
    string? ImageUrl = null,
    string? Tags = null,
    decimal Weight = 0,
    int LowStockThreshold = 10) : ICommand<CreateProductResponse>;

public sealed record CreateProductResponse(Guid ProductId, string Name, string Sku);
