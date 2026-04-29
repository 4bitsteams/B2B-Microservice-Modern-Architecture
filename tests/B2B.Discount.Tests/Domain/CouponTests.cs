using B2B.Discount.Domain.Entities;
using B2B.Discount.Domain.Events;
using FluentAssertions;
using Xunit;

namespace B2B.Discount.Tests.Domain;

public sealed class CouponTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Coupon NewCoupon(string code = "SAVE10", DiscountType type = DiscountType.Percentage,
        decimal value = 10m, int maxUsage = 3, decimal? minOrder = null, bool singleUse = false,
        DateTime? expires = null) =>
        Coupon.Create(code, "Save 10", type, value, TenantId,
            maxUsage, expires, minOrder, singleUse);

    // ── Creation ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldUppercaseCode()
    {
        var c = NewCoupon(code: "save10");

        c.Code.Should().Be("SAVE10");
    }

    [Fact]
    public void Create_ShouldRaiseCouponCreatedEvent()
    {
        var c = NewCoupon();

        c.DomainEvents.Should().ContainSingle(e => e is CouponCreatedEvent);
    }

    [Fact]
    public void Create_BlankCode_ShouldThrow()
    {
        var act = () => Coupon.Create("", "X", DiscountType.Percentage, 5m, TenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_BlankName_ShouldThrow()
    {
        var act = () => Coupon.Create("CODE", "", DiscountType.Percentage, 5m, TenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveValue_ShouldThrow(decimal value)
    {
        var act = () => Coupon.Create("CODE", "X", DiscountType.Percentage, value, TenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveMaxUsage_ShouldThrow(int maxUsage)
    {
        var act = () => Coupon.Create("CODE", "X", DiscountType.Percentage, 5m, TenantId, maxUsage);

        act.Should().Throw<ArgumentException>();
    }

    // ── IsAvailable / IsExpired ─────────────────────────────────────────────────

    [Fact]
    public void IsAvailable_New_ShouldBeTrue()
    {
        NewCoupon().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_PastDate_ShouldBeTrue()
    {
        var c = NewCoupon(expires: DateTime.UtcNow.AddMinutes(-1));

        c.IsExpired.Should().BeTrue();
        c.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_AtMaxUsage_ShouldBeFalse()
    {
        var c = NewCoupon(maxUsage: 1);
        c.Apply(100m);

        c.IsAvailable.Should().BeFalse();
    }

    // ── Apply ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Percentage_ShouldDiscount()
    {
        var c = NewCoupon(value: 20m);

        c.Apply(100m).Should().Be(80m);
    }

    [Fact]
    public void Apply_FixedAmount_ShouldSubtract()
    {
        var c = NewCoupon(type: DiscountType.FixedAmount, value: 15m);

        c.Apply(100m).Should().Be(85m);
    }

    [Fact]
    public void Apply_ShouldIncrementUsageAndRaiseEvent()
    {
        var c = NewCoupon();
        c.ClearDomainEvents();

        c.Apply(100m);

        c.UsageCount.Should().Be(1);
        c.DomainEvents.Should().ContainSingle(e => e is CouponUsedEvent);
    }

    [Fact]
    public void Apply_SingleUse_ShouldDeactivate()
    {
        var c = NewCoupon(singleUse: true);

        c.Apply(100m);

        c.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_BelowMinimumOrder_ShouldThrow()
    {
        var c = NewCoupon(minOrder: 50m);

        var act = () => c.Apply(40m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Order amount*");
    }

    [Fact]
    public void Apply_WhenUnavailable_ShouldThrow()
    {
        var c = NewCoupon(expires: DateTime.UtcNow.AddMinutes(-1));

        var act = () => c.Apply(100m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Deactivate_ShouldFlipFlag()
    {
        var c = NewCoupon();

        c.Deactivate();

        c.IsActive.Should().BeFalse();
    }
}
