using B2B.Discount.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Domain.Entities;

public sealed class Coupon : AggregateRoot<Guid>, IAuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public DiscountType Type { get; private set; }
    public decimal Value { get; private set; }
    public Guid TenantId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public int MaxUsageCount { get; private set; }
    public int UsageCount { get; private set; }
    public decimal? MinimumOrderAmount { get; private set; }
    public bool IsSingleUse { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsAvailable => IsActive && !IsExpired && UsageCount < MaxUsageCount;

    private Coupon() { }

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

    public decimal Apply(decimal orderAmount)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Coupon is not available.");
        if (MinimumOrderAmount.HasValue && orderAmount < MinimumOrderAmount.Value)
            throw new InvalidOperationException($"Order amount must be at least {MinimumOrderAmount:C}.");

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

    public void Deactivate() => IsActive = false;
}
