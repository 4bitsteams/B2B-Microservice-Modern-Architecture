using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Commands.CancelShipment;

public sealed record CancelShipmentCommand(Guid ShipmentId) : ICommand<CancelShipmentResponse>;
public sealed record CancelShipmentResponse(Guid ShipmentId, string Status);
