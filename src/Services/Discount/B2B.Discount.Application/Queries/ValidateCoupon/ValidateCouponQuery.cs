using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Queries.ValidateCoupon;

public sealed record ValidateCouponQuery(string Code, decimal OrderAmount) : IQuery<CouponValidationDto>;

public sealed record CouponValidationDto(
    bool IsValid,
    string? Code,
    string? Name,
    string? Type,
    decimal? DiscountValue,
    decimal? MinimumOrderAmount,
    DateTime? ExpiresAt,
    string? InvalidReason);
