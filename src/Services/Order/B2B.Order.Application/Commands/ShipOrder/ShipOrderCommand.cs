using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.ShipOrder;

public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber) : ICommand;
