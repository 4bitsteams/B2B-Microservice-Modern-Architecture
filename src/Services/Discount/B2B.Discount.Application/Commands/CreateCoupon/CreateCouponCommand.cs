using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.CreateCoupon;

public sealed record CreateCouponCommand(
    string Code,
    string Name,
    DiscountType Type,
    decimal Value,
    int MaxUsageCount = 1,
    DateTime? ExpiresAt = null,
    decimal? MinimumOrderAmount = null,
    bool IsSingleUse = false) : ICommand<CreateCouponResponse>;

public sealed record CreateCouponResponse(Guid CouponId, string Code, string Name);
