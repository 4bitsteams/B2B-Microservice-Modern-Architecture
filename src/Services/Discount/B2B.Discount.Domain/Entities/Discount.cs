using B2B.Discount.Domain.Events;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Domain.Entities;

/// <summary>
/// Aggregate root representing a tenant-scoped promotional discount.
///
/// <para>
/// A <see cref="Discount"/> applies a percentage or fixed-amount reduction
/// to eligible order amounts. Availability is governed by four concurrent
/// conditions: the discount must be active, within its date window, and under
/// its optional usage cap.
/// </para>
///
/// <para>
/// Differences from <see cref="Coupon"/>:
/// <list type="bullet">
///   <item><description>Discounts are applied automatically by the pricing engine — no code is required.</description></item>
///   <item><description>Discounts may be scoped to a specific product or category.</description></item>
///   <item><description>Coupons require an explicit redemption code and are usage-tracked per redemption.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class Discount : AggregateRoot<Guid>, IAuditableEntity
{
    /// <summary>Display name of the discount (e.g. "Summer Sale 20%").</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Optional long-form description shown to merchants in the admin UI.</summary>
    public string? Description { get; private set; }

    /// <summary>Whether the discount is percentage-based or a flat fixed amount.</summary>
    public DiscountType Type { get; private set; }

    /// <summary>
    /// Discount value. Interpreted as a percentage (0–100) for <see cref="DiscountType.Percentage"/>,
    /// or as a monetary amount for <see cref="DiscountType.FixedAmount"/>.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>Tenant that owns this discount; row-level isolation enforced by the global query filter.</summary>
    public Guid TenantId { get; private set; }

    /// <summary><see langword="true"/> when the discount has been manually activated.</summary>
    public bool IsActive { get; private set; }

    /// <summary>UTC date/time from which the discount becomes applicable. <see langword="null"/> means active immediately.</summary>
    public DateTime? StartDate { get; private set; }

    /// <summary>UTC date/time after which the discount expires. <see langword="null"/> means no expiry.</summary>
    public DateTime? EndDate { get; private set; }

    /// <summary>Minimum order subtotal required to qualify. <see langword="null"/> means no minimum.</summary>
    public decimal? MinimumOrderAmount { get; private set; }

    /// <summary>Maximum total redemptions allowed. <see langword="null"/> means unlimited.</summary>
    public int? MaxUsageCount { get; private set; }

    /// <summary>Number of times this discount has been redeemed.</summary>
    public int UsageCount { get; private set; }

    /// <summary>Restricts the discount to a single product. <see langword="null"/> means all products.</summary>
    public Guid? ApplicableProductId { get; private set; }

    /// <summary>Restricts the discount to a product category. <see langword="null"/> means all categories.</summary>
    public Guid? ApplicableCategoryId { get; private set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    /// <summary><see langword="true"/> when <see cref="EndDate"/> has passed.</summary>
    public bool IsExpired => EndDate.HasValue && EndDate.Value < DateTime.UtcNow;

    /// <summary><see langword="true"/> when the discount's start date has been reached (or no start date is set).</summary>
    public bool IsStarted => !StartDate.HasValue || StartDate.Value <= DateTime.UtcNow;

    /// <summary>
    /// <see langword="true"/> when all availability conditions are met:
    /// active, within the date window, and under the usage cap.
    /// </summary>
    public bool IsAvailable => IsActive && IsStarted && !IsExpired &&
        (!MaxUsageCount.HasValue || UsageCount < MaxUsageCount.Value);

    /// <summary>Parameterless constructor required by EF Core.</summary>
    private Discount() { }

    /// <summary>
    /// Creates and validates a new <see cref="Discount"/> aggregate.
    /// The discount is created in the active state and raises a <see cref="DiscountCreatedEvent"/>.
    /// </summary>
    /// <param name="name">Required display name.</param>
    /// <param name="type">Percentage or fixed amount.</param>
    /// <param name="value">Discount value — must be positive; ≤ 100 for percentage type.</param>
    /// <param name="tenantId">Owning tenant.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="startDate">Optional UTC start date.</param>
    /// <param name="endDate">Optional UTC expiry date.</param>
    /// <param name="minOrderAmount">Optional minimum order subtotal.</param>
    /// <param name="maxUsageCount">Optional redemption cap.</param>
    /// <param name="productId">Optional product scope.</param>
    /// <param name="categoryId">Optional category scope.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null/whitespace, <paramref name="value"/> ≤ 0,
    /// or a percentage <paramref name="value"/> exceeds 100.
    /// </exception>
    public static Discount Create(string name, DiscountType type, decimal value, Guid tenantId,
        string? description = null, DateTime? startDate = null, DateTime? endDate = null,
        decimal? minOrderAmount = null, int? maxUsageCount = null,
        Guid? productId = null, Guid? categoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (value <= 0) throw new ArgumentException("Discount value must be positive.", nameof(value));
        if (type == DiscountType.Percentage && value > 100)
            throw new ArgumentException("Percentage discount cannot exceed 100.", nameof(value));

        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Type = type,
            Value = value,
            TenantId = tenantId,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate,
            MinimumOrderAmount = minOrderAmount,
            MaxUsageCount = maxUsageCount,
            ApplicableProductId = productId,
            ApplicableCategoryId = categoryId
        };

        discount.RaiseDomainEvent(new DiscountCreatedEvent(discount.Id, name, type.ToString(), value));
        return discount;
    }

    /// <summary>
    /// Applies the discount to <paramref name="price"/> and increments the usage counter.
    /// </summary>
    /// <param name="price">The original price to discount.</param>
    /// <returns>
    /// The discounted price (rounded to two decimal places, never negative) on success;
    /// <c>Error.Validation</c> when the discount is not currently available.
    /// </returns>
    public Result<decimal> Apply(decimal price)
    {
        if (!IsAvailable)
            return Error.Validation("Discount.NotAvailable", "Discount is not available.");

        UsageCount++;

        return Type switch
        {
            DiscountType.Percentage  => Math.Round(price * (1 - Value / 100), 2),
            DiscountType.FixedAmount => Math.Max(0, Math.Round(price - Value, 2)),
            _ => Error.Validation("Discount.UnsupportedType", $"Discount type '{Type}' is not supported.")
        };
    }

    /// <summary>
    /// Deactivates the discount and raises a <see cref="DiscountDeactivatedEvent"/>.
    /// A deactivated discount cannot be applied until <see cref="Activate"/> is called.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        RaiseDomainEvent(new DiscountDeactivatedEvent(Id, Name));
    }

    /// <summary>Re-activates a previously deactivated discount.</summary>
    public void Activate() => IsActive = true;
}

/// <summary>Determines how a discount's <c>Value</c> is applied to an order price.</summary>
public enum DiscountType
{
    /// <summary>
    /// Reduces the price by a percentage of the original amount.
    /// <c>Value</c> is interpreted as 0–100 (e.g. <c>20</c> = 20% off).
    /// </summary>
    Percentage,

    /// <summary>
    /// Reduces the price by a flat monetary amount.
    /// The result is clamped to zero — the price cannot go negative.
    /// </summary>
    FixedAmount
}
