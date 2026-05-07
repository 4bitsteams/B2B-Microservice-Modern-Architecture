using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.ApplyCoupon;

/// <summary>
/// Command that redeems a coupon code against an order amount and returns
/// the discounted price.
///
/// <para>
/// The handler acquires a per-coupon distributed lock before the
/// check-then-act sequence to prevent concurrent over-redemption under
/// high traffic. See <c>ApplyCouponHandler</c> for details.
/// </para>
/// </summary>
/// <param name="CouponCode">The redemption code supplied by the customer (case-insensitive).</param>
/// <param name="OrderAmount">
/// The order subtotal to apply the coupon against.
/// Must be a positive value; the handler validates minimum-order-amount rules.
/// </param>
public sealed record ApplyCouponCommand(string CouponCode, decimal OrderAmount) : ICommand<ApplyCouponResponse>;

/// <summary>
/// Returned by <see cref="ApplyCouponHandler"/> on success.
/// </summary>
/// <param name="Code">The upper-cased coupon code that was applied.</param>
/// <param name="OriginalAmount">The order amount before the discount.</param>
/// <param name="DiscountedAmount">The order amount after the discount is applied.</param>
/// <param name="Savings">The absolute monetary saving (<c>OriginalAmount − DiscountedAmount</c>).</param>
public sealed record ApplyCouponResponse(
    string Code,
    decimal OriginalAmount,
    decimal DiscountedAmount,
    decimal Savings);
