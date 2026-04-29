using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Commands.DeactivateDiscount;

public sealed class DeactivateDiscountHandler(
    IDiscountRepository discountRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateDiscountCommand, DeactivateDiscountResponse>
{
    public async Task<Result<DeactivateDiscountResponse>> Handle(DeactivateDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = await discountRepository.GetByIdAsync(request.DiscountId, cancellationToken);
        if (discount is null || discount.TenantId != currentUser.TenantId)
            return Error.NotFound("Discount.NotFound", $"Discount {request.DiscountId} not found.");

        if (!discount.IsActive)
            return Error.Conflict("Discount.AlreadyInactive", "Discount is already inactive.");

        discount.Deactivate();
        discountRepository.Update(discount);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeactivateDiscountResponse(discount.Id, discount.IsActive);
    }
}
