using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Discount.Application.Commands.CreateCoupon;

/// <summary>
/// Command that issues a new coupon code for the current tenant.
///
/// <para>
/// Coupon codes are upper-cased on creation. The <see cref="Code"/> must be
/// unique within the tenant — <c>CreateCouponHandler</c> returns
/// <c>Error.Conflict</c> if a duplicate code is detected.
/// </para>
/// </summary>
/// <param name="Code">
/// The redemption code (case-insensitive, stored upper-cased, max 50 chars).
/// Must be unique per tenant.
/// </param>
/// <param name="Name">Display name shown in the admin UI and on receipts (max 200 chars).</param>
/// <param name="Type">Percentage or fixed-amount discount.</param>
/// <param name="Value">Discount magnitude — must be &gt; 0.</param>
/// <param name="MaxUsageCount">Total redemptions allowed across all customers. Defaults to 1.</param>
/// <param name="ExpiresAt">Optional UTC expiry date/time.</param>
/// <param name="MinimumOrderAmount">Optional minimum order subtotal required to qualify.</param>
/// <param name="IsSingleUse">
/// When <see langword="true"/>, the coupon is deactivated after its first successful
/// redemption regardless of <paramref name="MaxUsageCount"/>.
/// </param>
public sealed record CreateCouponCommand(
    string Code,
    string Name,
    DiscountType Type,
    decimal Value,
    int MaxUsageCount = 1,
    DateTime? ExpiresAt = null,
    decimal? MinimumOrderAmount = null,
    bool IsSingleUse = false) : ICommand<CreateCouponResponse>;

/// <summary>
/// Returned by <see cref="CreateCouponHandler"/> on success.
/// </summary>
/// <param name="CouponId">Persisted identifier of the new coupon.</param>
/// <param name="Code">The stored (upper-cased) redemption code.</param>
/// <param name="Name">Display name of the coupon.</param>
public sealed record CreateCouponResponse(Guid CouponId, string Code, string Name);
