using B2B.Shared.Core.Domain;

namespace B2B.Order.Domain.Events;

public sealed record OrderConfirmedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDetails> Items) : DomainEvent;

public sealed record OrderShippedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string TrackingNumber) : DomainEvent;

public sealed record OrderDeliveredEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId) : DomainEvent;

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string Reason) : DomainEvent;

public sealed record OrderItemDetails(Guid ProductId, string Sku, int Quantity);
