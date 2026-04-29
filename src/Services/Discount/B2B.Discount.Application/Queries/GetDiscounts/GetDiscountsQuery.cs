using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Queries.GetDiscounts;

public sealed record GetDiscountsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<DiscountDto>>;

public sealed record DiscountDto(
    Guid Id,
    string Name,
    string Type,
    decimal Value,
    bool IsActive,
    bool IsAvailable,
    DateTime? StartDate,
    DateTime? EndDate,
    int UsageCount,
    int? MaxUsageCount);
