using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Basket.Application.Commands.AddToBasket;

public sealed class AddToBasketHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser)
    : ICommandHandler<AddToBasketCommand, BasketResponse>
{
    public async Task<Result<BasketResponse>> Handle(AddToBasketCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketRepository.GetOrCreateAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);

        basket.AddItem(request.ProductId, request.ProductName, request.Sku,
            request.UnitPrice, request.Quantity, request.ImageUrl);

        await basketRepository.SaveAsync(basket, cancellationToken);

        return new BasketResponse(basket.Id, basket.CustomerId, basket.TotalItems, basket.TotalPrice);
    }
}
