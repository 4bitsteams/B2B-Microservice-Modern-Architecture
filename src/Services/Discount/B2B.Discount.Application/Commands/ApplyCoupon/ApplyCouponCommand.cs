using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.ApplyCoupon;

public sealed record ApplyCouponCommand(string CouponCode, decimal OrderAmount) : ICommand<ApplyCouponResponse>;

public sealed record ApplyCouponResponse(string Code, decimal OriginalAmount, decimal DiscountedAmount, decimal Savings);
