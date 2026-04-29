using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Commands.CreateDiscount;

public sealed class CreateDiscountHandler(
    IDiscountRepository discountRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDiscountCommand, CreateDiscountResponse>
{
    public async Task<Result<CreateDiscountResponse>> Handle(CreateDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = DiscountEntity.Create(
            request.Name, request.Type, request.Value, currentUser.TenantId,
            request.Description, request.StartDate, request.EndDate,
            request.MinimumOrderAmount, request.MaxUsageCount,
            request.ApplicableProductId, request.ApplicableCategoryId);

        await discountRepository.AddAsync(discount, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDiscountResponse(discount.Id, discount.Name, discount.Type.ToString(), discount.Value);
    }
}
