using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Basket.Application.Queries.GetBasket;

public sealed class GetBasketHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetBasketQuery, Result<BasketDto>>
{
    public async Task<Result<BasketDto>> Handle(GetBasketQuery request, CancellationToken cancellationToken)
    {
        var basket = await basketRepository.GetAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);
        if (basket is null)
            return Error.NotFound("Basket.NotFound", "No active basket found.");

        return new BasketDto(
            basket.Id,
            basket.CustomerId,
            basket.Items.Select(i => new BasketItemDto(
                i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.TotalPrice, i.ImageUrl)).ToList(),
            basket.TotalPrice,
            basket.TotalItems,
            basket.LastModified);
    }
}
