using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;
