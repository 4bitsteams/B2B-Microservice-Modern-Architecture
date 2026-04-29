using B2B.Shared.Core.CQRS;

namespace B2B.Basket.Application.Commands.AddToBasket;

public sealed record AddToBasketCommand(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    string? ImageUrl = null) : ICommand<BasketResponse>;

public sealed record BasketResponse(
    Guid BasketId,
    Guid CustomerId,
    int TotalItems,
    decimal TotalPrice);
