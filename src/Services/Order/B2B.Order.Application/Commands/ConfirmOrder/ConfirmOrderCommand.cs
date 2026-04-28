using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.ConfirmOrder;

public sealed record ConfirmOrderCommand(Guid OrderId) : ICommand;
