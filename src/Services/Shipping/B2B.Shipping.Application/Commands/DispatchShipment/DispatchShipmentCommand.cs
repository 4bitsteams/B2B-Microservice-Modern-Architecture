using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Commands.DispatchShipment;

public sealed record DispatchShipmentCommand(Guid ShipmentId) : ICommand;
