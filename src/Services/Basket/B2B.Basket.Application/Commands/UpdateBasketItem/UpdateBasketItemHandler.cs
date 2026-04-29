using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Basket.Application.Commands.UpdateBasketItem;

public sealed class UpdateBasketItemHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateBasketItemCommand, BasketResponse>
{
    public async Task<Result<BasketResponse>> Handle(UpdateBasketItemCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketRepository.GetAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);
        if (basket is null)
            return Error.NotFound("Basket.NotFound", "Basket not found.");

        try
        {
            basket.UpdateItemQuantity(request.ProductId, request.Quantity);
        }
        catch (InvalidOperationException ex)
        {
            return Error.NotFound("Basket.ItemNotFound", ex.Message);
        }

        await basketRepository.SaveAsync(basket, cancellationToken);

        return new BasketResponse(basket.Id, basket.CustomerId, basket.TotalItems, basket.TotalPrice);
    }
}
