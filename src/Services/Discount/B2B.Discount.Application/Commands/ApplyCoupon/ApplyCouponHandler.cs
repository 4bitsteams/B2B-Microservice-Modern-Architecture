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

        if (!coupon.IsAvailable)
            return Error.Validation("Coupon.NotAvailable", "This coupon is not available or has expired.");

        decimal discounted;
        try { discounted = coupon.Apply(request.OrderAmount); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Coupon.InvalidApplication", ex.Message); }

        couponRepository.Update(coupon);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyCouponResponse(coupon.Code, request.OrderAmount, discounted,
            Math.Round(request.OrderAmount - discounted, 2));
    }
}
