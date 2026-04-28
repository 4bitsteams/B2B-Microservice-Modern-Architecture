using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDetailDto>;

public sealed record OrderDetailDto(
    Guid OrderId,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    decimal TaxAmount,
    decimal ShippingCost,
    decimal TotalAmount,
    int ItemCount,
    AddressDetailDto ShippingAddress,
    AddressDetailDto? BillingAddress,
    string? Notes,
    string? TrackingNumber,
    IReadOnlyList<OrderItemDetailDto> Items,
    DateTime CreatedAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancellationReason);

public sealed record AddressDetailDto(
    string Street, string City, string State,
    string PostalCode, string Country);

public sealed record OrderItemDetailDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);
