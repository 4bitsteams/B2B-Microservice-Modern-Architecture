using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Basket.Application.Commands.ClearBasket;

public sealed class ClearBasketHandler(
    IBasketRepository basketRepository,
    ICurrentUser currentUser)
    : ICommandHandler<ClearBasketCommand>
{
    public async Task<Result> Handle(ClearBasketCommand request, CancellationToken cancellationToken)
    {
        await basketRepository.DeleteAsync(currentUser.UserId, currentUser.TenantId, cancellationToken);
        return Result.Success();
    }
}
