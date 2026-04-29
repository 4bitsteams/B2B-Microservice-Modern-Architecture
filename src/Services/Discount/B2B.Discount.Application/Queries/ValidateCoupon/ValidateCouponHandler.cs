using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Discount.Application.Queries.ValidateCoupon;

public sealed class ValidateCouponHandler(
    IReadCouponRepository couponRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ValidateCouponQuery, Result<CouponValidationDto>>
{
    public async Task<Result<CouponValidationDto>> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var coupons = await couponRepository.FindAsync(
            c => c.Code == request.Code.ToUpperInvariant() && c.TenantId == currentUser.TenantId,
            cancellationToken);
        var coupon = coupons.FirstOrDefault();

        if (coupon is null)
            return new CouponValidationDto(false, request.Code, null, null, null, null, null, "Coupon not found.");

        if (!coupon.IsAvailable)
            return new CouponValidationDto(false, coupon.Code, coupon.Name, coupon.Type.ToString(),
                coupon.Value, coupon.MinimumOrderAmount, coupon.ExpiresAt,
                coupon.IsExpired ? "Coupon has expired." : "Coupon is no longer available.");

        if (coupon.MinimumOrderAmount.HasValue && request.OrderAmount < coupon.MinimumOrderAmount.Value)
            return new CouponValidationDto(false, coupon.Code, coupon.Name, coupon.Type.ToString(),
                coupon.Value, coupon.MinimumOrderAmount, coupon.ExpiresAt,
                $"Minimum order amount of {coupon.MinimumOrderAmount:C} required.");

        return new CouponValidationDto(true, coupon.Code, coupon.Name, coupon.Type.ToString(),
            coupon.Value, coupon.MinimumOrderAmount, coupon.ExpiresAt, null);
    }
}
