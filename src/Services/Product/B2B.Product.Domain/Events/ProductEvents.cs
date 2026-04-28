using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Domain;

namespace B2B.Product.Domain.Events;

public sealed record ProductCreatedEvent(Guid ProductId, string Name, Guid TenantId) : DomainEvent;
public sealed record ProductPriceChangedEvent(Guid ProductId, Money? OldPrice, Money NewPrice) : DomainEvent;
public sealed record ProductStockChangedEvent(Guid ProductId, int PreviousQuantity, int NewQuantity, string Reason) : DomainEvent;
public sealed record ProductLowStockEvent(Guid ProductId, string ProductName, int CurrentStock, int Threshold) : DomainEvent;
