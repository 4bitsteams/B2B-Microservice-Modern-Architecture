using B2B.Discount.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Domain.Entities;

public sealed class Discount : AggregateRoot<Guid>, IAuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DiscountType Type { get; private set; }
    public decimal Value { get; private set; }
    public Guid TenantId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public decimal? MinimumOrderAmount { get; private set; }
    public int? MaxUsageCount { get; private set; }
    public int UsageCount { get; private set; }
    public Guid? ApplicableProductId { get; private set; }
    public Guid? ApplicableCategoryId { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsExpired => EndDate.HasValue && EndDate.Value < DateTime.UtcNow;
    public bool IsStarted => !StartDate.HasValue || StartDate.Value <= DateTime.UtcNow;
    public bool IsAvailable => IsActive && IsStarted && !IsExpired &&
        (!MaxUsageCount.HasValue || UsageCount < MaxUsageCount.Value);

    private Discount() { }

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

    public decimal Apply(decimal price)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Discount is not available.");

        UsageCount++;

        return Type switch
        {
            DiscountType.Percentage  => Math.Round(price * (1 - Value / 100), 2),
            DiscountType.FixedAmount => Math.Max(0, Math.Round(price - Value, 2)),
            _ => throw new NotSupportedException($"Discount type '{Type}' is not supported.")
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        RaiseDomainEvent(new DiscountDeactivatedEvent(Id, Name));
    }

    public void Activate() => IsActive = true;
}

public enum DiscountType { Percentage, FixedAmount }
