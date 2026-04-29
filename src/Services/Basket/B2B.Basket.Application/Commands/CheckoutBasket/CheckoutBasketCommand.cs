using B2B.Shared.Core.CQRS;

namespace B2B.Basket.Application.Commands.CheckoutBasket;

public sealed record CheckoutBasketCommand(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Notes = null) : ICommand<CheckoutBasketResponse>;

public sealed record CheckoutBasketResponse(
    Guid CustomerId,
    decimal TotalAmount,
    int ItemCount);
