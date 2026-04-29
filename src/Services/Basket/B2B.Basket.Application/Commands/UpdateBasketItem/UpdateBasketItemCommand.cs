using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Shared.Core.CQRS;

namespace B2B.Basket.Application.Commands.UpdateBasketItem;

public sealed record UpdateBasketItemCommand(Guid ProductId, int Quantity) : ICommand<BasketResponse>;
