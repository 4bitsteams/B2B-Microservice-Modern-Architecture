using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Application.Commands.ApplyCoupon;

public sealed class ApplyCouponHandler(
    ICouponRepository couponRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyCouponCommand, ApplyCouponResponse>
{
    public async Task<Result<ApplyCouponResponse>> Handle(ApplyCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await couponRepository.GetByCodeAsync(request.CouponCode, currentUser.TenantId, cancellationToken);
        if (coupon is null)
            return Error.NotFound("Coupon.NotFound", $"Coupon '{request.CouponCode}' not found.");

        var applyResult = coupon.Apply(request.OrderAmount);
        if (applyResult.IsFailure)
            return applyResult.Error;

        couponRepository.Update(coupon);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyCouponResponse(coupon.Code, request.OrderAmount, applyResult.Value,
            Math.Round(request.OrderAmount - applyResult.Value, 2));
    }
}
