using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.CreateDiscount;

public sealed record CreateDiscountCommand(
    string Name,
    DiscountType Type,
    decimal Value,
    string? Description = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal? MinimumOrderAmount = null,
    int? MaxUsageCount = null,
    Guid? ApplicableProductId = null,
    Guid? ApplicableCategoryId = null) : ICommand<CreateDiscountResponse>;

public sealed record CreateDiscountResponse(Guid DiscountId, string Name, string Type, decimal Value);
