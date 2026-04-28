using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.DeliverOrder;

public sealed record DeliverOrderCommand(Guid OrderId) : ICommand;
