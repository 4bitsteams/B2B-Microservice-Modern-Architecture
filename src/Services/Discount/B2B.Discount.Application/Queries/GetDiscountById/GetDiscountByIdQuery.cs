using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Queries.GetDiscountById;

public sealed record GetDiscountByIdQuery(Guid DiscountId) : IQuery<DiscountDetailDto>;

public sealed record DiscountDetailDto(
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
    Guid? ApplicableProductId,
    Guid? ApplicableCategoryId,
    DateTime CreatedAt);
