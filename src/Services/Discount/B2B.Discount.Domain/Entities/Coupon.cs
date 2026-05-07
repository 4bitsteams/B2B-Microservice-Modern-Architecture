using B2B.Discount.Domain.Events;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Domain.Entities;

/// <summary>
/// Aggregate root representing a redeemable promotional coupon code.
///
/// <para>
/// A coupon differs from a <see cref="Discount"/> in that it requires the customer to
/// enter an explicit <see cref="Code"/> at checkout. Discounts are applied automatically
/// by the pricing engine; coupons are applied on-demand via <c>ApplyCouponCommand</c>.
/// </para>
///
/// <para>
/// Availability is governed by three independent guards (all must pass):
/// <list type="bullet">
///   <item><description><see cref="IsActive"/> — not manually deactivated.</description></item>
///   <item><description><see cref="IsExpired"/> — <see cref="ExpiresAt"/> has not yet passed.</description></item>
///   <item><description><see cref="UsageCount"/> &lt; <see cref="MaxUsageCount"/> — redemption budget remaining.</description></item>
/// </list>
/// Use <see cref="IsAvailable"/> to check all three at once.
/// </para>
///
/// <para>
/// Concurrency: <see cref="Apply"/> increments <see cref="UsageCount"/> non-atomically in
/// the domain model. Callers (<c>ApplyCouponHandler</c>) must hold an <c>IDistributedLock</c>
/// scoped to the coupon code + tenant before invoking <see cref="Apply"/> to prevent
/// double-redemption under high concurrency.
/// </para>
/// </summary>
public sealed class Coupon : AggregateRoot<Guid>, IAuditableEntity
{
    /// <summary>
    /// Unique redemption code for this coupon, always stored in upper-case.
    /// Scoped to <see cref="TenantId"/> — the same code string may exist in different tenants.
    /// </summary>
    public string Code { get; private set; } = default!;

    /// <summary>Human-readable display name shown to customers (e.g. "Summer Sale 20%").</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Determines the discount calculation: percentage off or fixed amount off.</summary>
    public DiscountType Type { get; private set; }

    /// <summary>
    /// Numeric discount value. Interpreted according to <see cref="Type"/>:
    /// <list type="bullet">
    ///   <item><description><see cref="DiscountType.Percentage"/> — percentage points (e.g. <c>20</c> = 20 % off).</description></item>
    ///   <item><description><see cref="DiscountType.FixedAmount"/> — absolute currency amount to subtract.</description></item>
    /// </list>
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>Tenant that owns this coupon. Coupons are never shared across tenants.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Whether the coupon is administratively active.
    /// Set to <see langword="false"/> by <see cref="Deactivate"/> or automatically
    /// by <see cref="Apply"/> when <see cref="IsSingleUse"/> is <see langword="true"/>.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Optional UTC expiry. When <see langword="null"/>, the coupon never expires by date.</summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Maximum total number of times this coupon may be redeemed across all customers.</summary>
    public int MaxUsageCount { get; private set; }

    /// <summary>Current number of successful redemptions. Incremented by <see cref="Apply"/>.</summary>
    public int UsageCount { get; private set; }

    /// <summary>
    /// Optional minimum subtotal required to apply the coupon. <see langword="null"/> means no minimum.
    /// </summary>
    public decimal? MinimumOrderAmount { get; private set; }

    /// <summary>
    /// When <see langword="true"/>, <see cref="Apply"/> deactivates the coupon on first successful
    /// redemption by setting <see cref="IsActive"/> to <see langword="false"/>.
    /// </summary>
    public bool IsSingleUse { get; private set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="ExpiresAt"/> is set and has already passed (UTC).
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    /// <summary>
    /// <see langword="true"/> when the coupon is active, not expired, and has remaining usage budget.
    /// This is the single guard that <see cref="Apply"/> evaluates first.
    /// </summary>
    public bool IsAvailable => IsActive && !IsExpired && UsageCount < MaxUsageCount;

    private Coupon() { }

    /// <summary>
    /// Factory method — the only public way to construct a valid <see cref="Coupon"/>.
    /// The code is normalized to upper-case; raises <see cref="CouponCreatedEvent"/>.
    /// </summary>
    /// <param name="code">Redemption code (case-insensitive; stored upper-cased).</param>
    /// <param name="name">Display name shown to customers.</param>
    /// <param name="type">Discount calculation type (percentage or fixed amount).</param>
    /// <param name="value">Discount magnitude. Must be positive.</param>
    /// <param name="tenantId">Owning tenant. Uniqueness of <paramref name="code"/> is scoped to this tenant.</param>
    /// <param name="maxUsageCount">Total redemption budget. Defaults to 1 (single-use by count).</param>
    /// <param name="expiresAt">Optional UTC expiry date. Pass <see langword="null"/> for no expiry.</param>
    /// <param name="minOrderAmount">Optional minimum order subtotal required. Pass <see langword="null"/> for no minimum.</param>
    /// <param name="isSingleUse">
    /// When <see langword="true"/>, the coupon deactivates after first successful redemption
    /// regardless of <paramref name="maxUsageCount"/>.
    /// </param>
    /// <returns>A new active <see cref="Coupon"/> with <see cref="UsageCount"/> = 0.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="code"/> or <paramref name="name"/> is null/whitespace,
    /// <paramref name="value"/> ≤ 0, or <paramref name="maxUsageCount"/> ≤ 0.
    /// </exception>
    public static Coupon Create(string code, string name, DiscountType type, decimal value,
        Guid tenantId, int maxUsageCount = 1, DateTime? expiresAt = null,
        decimal? minOrderAmount = null, bool isSingleUse = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (value <= 0) throw new ArgumentException("Coupon value must be positive.", nameof(value));
        if (maxUsageCount <= 0) throw new ArgumentException("Max usage count must be positive.", nameof(maxUsageCount));

        var coupon = new Coupon
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant(),
            Name = name,
            Type = type,
            Value = value,
            TenantId = tenantId,
            IsActive = true,
            ExpiresAt = expiresAt,
            MaxUsageCount = maxUsageCount,
            MinimumOrderAmount = minOrderAmount,
            IsSingleUse = isSingleUse
        };

        coupon.RaiseDomainEvent(new CouponCreatedEvent(coupon.Id, coupon.Code, coupon.Name));
        return coupon;
    }

    /// <summary>
    /// Redeems the coupon against <paramref name="orderAmount"/> and returns the discounted total.
    /// Increments <see cref="UsageCount"/>; deactivates the coupon when <see cref="IsSingleUse"/>
    /// is <see langword="true"/>. Raises <see cref="CouponUsedEvent"/>.
    /// </summary>
    /// <param name="orderAmount">Pre-discount order subtotal (must be ≥ 0).</param>
    /// <returns>
    /// The discounted order total on success; <c>Error.Validation</c> when
    /// the coupon is unavailable or the order does not meet the minimum amount threshold.
    /// The returned amount is always ≥ 0 (fixed-amount coupons clamp to zero).
    /// </returns>
    public Result<decimal> Apply(decimal orderAmount)
    {
        if (!IsAvailable)
            return Error.Validation("Coupon.NotAvailable", "Coupon is not available or has expired.");
        if (MinimumOrderAmount.HasValue && orderAmount < MinimumOrderAmount.Value)
            return Error.Validation("Coupon.MinimumOrderAmount", $"Order amount must be at least {MinimumOrderAmount:C}.");

        UsageCount++;
        if (IsSingleUse) IsActive = false;

        RaiseDomainEvent(new CouponUsedEvent(Id, Code));

        return Type switch
        {
            DiscountType.Percentage => Math.Round(orderAmount * (1 - Value / 100), 2),
            DiscountType.FixedAmount => Math.Max(0, Math.Round(orderAmount - Value, 2)),
            _ => orderAmount
        };
    }

    /// <summary>
    /// Administratively deactivates the coupon so it can no longer be redeemed,
    /// regardless of remaining usage budget or expiry date.
    /// </summary>
    public void Deactivate() => IsActive = false;
}
