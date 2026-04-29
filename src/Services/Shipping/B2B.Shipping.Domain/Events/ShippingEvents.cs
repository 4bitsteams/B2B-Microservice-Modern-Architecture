using B2B.Shared.Core.Domain;

namespace B2B.Shipping.Domain.Events;

public sealed record ShipmentCreatedEvent(Guid ShipmentId, Guid OrderId, string TrackingNumber) : DomainEvent;
public sealed record ShipmentDispatchedEvent(Guid ShipmentId, Guid OrderId, string TrackingNumber, string Carrier) : DomainEvent;
public sealed record ShipmentDeliveredEvent(Guid ShipmentId, Guid OrderId, DateTime DeliveredAt) : DomainEvent;
