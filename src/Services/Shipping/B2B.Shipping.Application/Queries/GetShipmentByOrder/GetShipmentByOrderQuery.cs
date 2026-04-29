using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Queries.GetShipmentByOrder;

public sealed record GetShipmentByOrderQuery(Guid OrderId) : IQuery<ShipmentDto>;

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string TrackingNumber,
    string Status,
    string Carrier,
    string RecipientName,
    decimal ShippingCost,
    string? EstimatedDelivery,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime CreatedAt);
