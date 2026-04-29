using B2B.Shared.Core.CQRS;

namespace B2B.Basket.Application.Commands.RemoveFromBasket;

public sealed record RemoveFromBasketCommand(Guid ProductId) : ICommand;
