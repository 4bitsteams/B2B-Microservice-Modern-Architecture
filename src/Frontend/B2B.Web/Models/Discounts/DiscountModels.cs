namespace B2B.Web.Models.Discounts;

public sealed record DiscountDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    decimal Value,
    bool IsActive,
    bool IsAvailable,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? MinimumOrderAmount,
    int? MaxUsageCount,
    int UsageCount,
    DateTime CreatedAt);

public sealed record CouponDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    decimal Value,
    bool IsActive,
    bool IsAvailable,
    int UsageCount,
    int MaxUsageCount,
    DateTime? ExpiresAt,
    decimal? MinimumOrderAmount,
    bool IsSingleUse,
    DateTime CreatedAt);

public sealed record CreateDiscountRequest(
    string Name,
    string Type,
    decimal Value,
    string? Description = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal? MinimumOrderAmount = null,
    int? MaxUsageCount = null);

public sealed record CreateCouponRequest(
    string Code,
    string Name,
    string Type,
    decimal Value,
    int MaxUsageCount = 1,
    DateTime? ExpiresAt = null,
    decimal? MinimumOrderAmount = null,
    bool IsSingleUse = false);

public sealed record ApplyCouponRequest(string CouponCode, decimal OrderAmount);

public sealed record ApplyCouponResponse(
    string Code,
    decimal OriginalAmount,
    decimal DiscountedAmount,
    decimal Savings);
