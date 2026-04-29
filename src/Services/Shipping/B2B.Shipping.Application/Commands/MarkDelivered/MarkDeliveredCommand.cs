using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Commands.MarkDelivered;

public sealed record MarkDeliveredCommand(Guid ShipmentId) : ICommand;
