using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Commands.CreateShipment;

public sealed record CreateShipmentCommand(
    Guid OrderId,
    string Carrier,
    string RecipientName,
    string Address,
    string City,
    string Country,
    decimal ShippingCost,
    string? EstimatedDelivery = null) : ICommand<CreateShipmentResponse>;

public sealed record CreateShipmentResponse(
    Guid ShipmentId,
    string TrackingNumber,
    string Status);
