using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    IReadOnlyList<OrderItemRequest> Items,
    string? Notes = null) : ICommand<CreateOrderResponse>, IIdempotentCommand
{
    public string IdempotencyKey { get; init; } = string.Empty;
}

public sealed record AddressDto(
    string Street, string City, string State,
    string PostalCode, string Country);

public sealed record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);

public sealed record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    string Status);
