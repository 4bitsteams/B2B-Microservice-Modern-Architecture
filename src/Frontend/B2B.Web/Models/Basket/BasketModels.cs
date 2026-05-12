namespace B2B.Web.Models.Basket;

public sealed record BasketDto(
    Guid Id,
    List<BasketItemDto> Items,
    decimal TotalAmount,
    int TotalItems);

public sealed record BasketItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal SubTotal);

public sealed record AddToBasketRequest(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);

public sealed record UpdateBasketItemRequest(int Quantity);
