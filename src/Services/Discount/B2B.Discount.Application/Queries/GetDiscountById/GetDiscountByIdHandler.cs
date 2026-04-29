using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Application.Queries.GetDiscountById;

public sealed class GetDiscountByIdHandler(
    IReadDiscountRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetDiscountByIdQuery, DiscountDetailDto>
{
    public async Task<Result<DiscountDetailDto>> Handle(GetDiscountByIdQuery request, CancellationToken cancellationToken)
    {
        var discount = await readRepository.GetByIdAsync(request.DiscountId, cancellationToken);
        if (discount is null || discount.TenantId != currentUser.TenantId)
            return Error.NotFound("Discount.NotFound", $"Discount {request.DiscountId} not found.");

        return new DiscountDetailDto(
            discount.Id,
            discount.Name,
            discount.Description,
            discount.Type.ToString(),
            discount.Value,
            discount.IsActive,
            discount.IsAvailable,
            discount.StartDate,
            discount.EndDate,
            discount.MinimumOrderAmount,
            discount.MaxUsageCount,
            discount.UsageCount,
            discount.ApplicableProductId,
            discount.ApplicableCategoryId,
            discount.CreatedAt);
    }
}
