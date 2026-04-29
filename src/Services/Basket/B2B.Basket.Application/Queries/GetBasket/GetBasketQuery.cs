using B2B.Shared.Core.CQRS;

namespace B2B.Basket.Application.Queries.GetBasket;

public sealed record GetBasketQuery : IQuery<BasketDto>;

public sealed record BasketDto(
    Guid BasketId,
    Guid CustomerId,
    IReadOnlyList<BasketItemDto> Items,
    decimal TotalPrice,
    int TotalItems,
    DateTime LastModified);

public sealed record BasketItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice,
    string? ImageUrl);
