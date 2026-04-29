using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Application.Commands.CreateCoupon;

public sealed class CreateCouponHandler(
    ICouponRepository couponRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCouponCommand, CreateCouponResponse>
{
    public async Task<Result<CreateCouponResponse>> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var existing = await couponRepository.GetByCodeAsync(request.Code, currentUser.TenantId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Coupon.CodeExists", $"Coupon code '{request.Code}' already exists.");

        var coupon = Coupon.Create(request.Code, request.Name, request.Type, request.Value,
            currentUser.TenantId, request.MaxUsageCount, request.ExpiresAt,
            request.MinimumOrderAmount, request.IsSingleUse);

        await couponRepository.AddAsync(coupon, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCouponResponse(coupon.Id, coupon.Code, coupon.Name);
    }
}
