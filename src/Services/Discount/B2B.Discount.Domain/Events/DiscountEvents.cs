using B2B.Shared.Core.Domain;

namespace B2B.Discount.Domain.Events;

/// <summary>
/// Raised by <c>Discount.Create()</c> when a new discount is created.
/// Consumed downstream to sync analytics or populate search indexes.
/// </summary>
/// <param name="DiscountId">Unique identifier of the created discount.</param>
/// <param name="Name">Display name of the discount.</param>
/// <param name="Type">Discount type string ("Percentage" or "FixedAmount").</param>
/// <param name="Value">The discount value at creation time.</param>
public sealed record DiscountCreatedEvent(Guid DiscountId, string Name, string Type, decimal Value) : DomainEvent;

/// <summary>
/// Raised by <c>Discount.Deactivate()</c> when a discount is deactivated.
/// Consumed to purge cached pricing rules and notify affected merchants.
/// </summary>
/// <param name="DiscountId">Unique identifier of the deactivated discount.</param>
/// <param name="Name">Display name of the deactivated discount.</param>
public sealed record DiscountDeactivatedEvent(Guid DiscountId, string Name) : DomainEvent;

/// <summary>
/// Raised by <c>Coupon.Create()</c> when a new coupon code is issued.
/// </summary>
/// <param name="CouponId">Unique identifier of the created coupon.</param>
/// <param name="Code">The redemption code (always upper-cased).</param>
/// <param name="Name">Display name of the coupon.</param>
public sealed record CouponCreatedEvent(Guid CouponId, string Code, string Name) : DomainEvent;

/// <summary>
/// Raised by <c>Coupon.Apply()</c> each time a coupon is successfully redeemed.
/// Consumed to update analytics, send confirmation emails, or trigger loyalty rewards.
/// </summary>
/// <param name="CouponId">Unique identifier of the used coupon.</param>
/// <param name="Code">The redemption code that was applied.</param>
public sealed record CouponUsedEvent(Guid CouponId, string Code) : DomainEvent;
