using B2B.Shared.Core.Domain;

namespace B2B.Basket.Domain.Events;

public sealed record ItemAddedToBasketEvent(Guid CustomerId, Guid ProductId, int Quantity) : DomainEvent;

public sealed record ItemRemovedFromBasketEvent(Guid CustomerId, Guid ProductId) : DomainEvent;

public sealed record BasketCheckedOutEvent(
    Guid CustomerId,
    Guid TenantId,
    decimal TotalAmount,
    IReadOnlyList<BasketItemSnapshot> Items) : DomainEvent;

public sealed record BasketItemSnapshot(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);
