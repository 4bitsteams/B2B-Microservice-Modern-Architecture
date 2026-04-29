using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Basket.Application.Commands.CheckoutBasket;

public sealed class CheckoutBasketHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser,
    IEventBus eventBus)
    : ICommandHandler<CheckoutBasketCommand, CheckoutBasketResponse>
{
    public async Task<Result<CheckoutBasketResponse>> Handle(CheckoutBasketCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketRepository.GetAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);
        if (basket is null || basket.Items.Count == 0)
            return Error.Validation("Basket.Empty", "Basket is empty or does not exist.");

        basket.Checkout();

        // Publish integration event so Order service can create the order
        await eventBus.PublishAsync(new BasketCheckedOutIntegration(
            basket.CustomerId,
            basket.TenantId,
            request.Street, request.City, request.State, request.PostalCode, request.Country,
            request.Notes,
            currentUser.Email,
            basket.Items.Select(i => new BasketItemIntegration(i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity)).ToList(),
            basket.TotalPrice), cancellationToken);

        // Remove basket after checkout
        await basketRepository.DeleteAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);

        return new CheckoutBasketResponse(basket.CustomerId, basket.TotalPrice, basket.TotalItems);
    }
}
