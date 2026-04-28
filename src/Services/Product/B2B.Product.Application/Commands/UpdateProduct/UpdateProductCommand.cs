using B2B.Shared.Core.CQRS;

namespace B2B.Product.Application.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    string? ImageUrl,
    string? Tags) : ICommand;
