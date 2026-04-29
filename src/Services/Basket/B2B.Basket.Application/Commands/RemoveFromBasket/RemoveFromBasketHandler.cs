using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Basket.Application.Commands.RemoveFromBasket;

public sealed class RemoveFromBasketHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser)
    : ICommandHandler<RemoveFromBasketCommand>
{
    public async Task<Result> Handle(RemoveFromBasketCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketRepository.GetAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);
        if (basket is null)
            return Error.NotFound("Basket.NotFound", "Basket not found.");

        try
        {
            basket.RemoveItem(request.ProductId);
        }
        catch (InvalidOperationException ex)
        {
            return Error.NotFound("Basket.ItemNotFound", ex.Message);
        }

        await basketRepository.SaveAsync(basket, cancellationToken);
        return Result.Success();
    }
}
