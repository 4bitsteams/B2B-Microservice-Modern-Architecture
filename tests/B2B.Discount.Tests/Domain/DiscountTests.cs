using B2B.Discount.Domain.Entities;
using B2B.Discount.Domain.Events;
using FluentAssertions;
using Xunit;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Tests.Domain;

public sealed class DiscountTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static DiscountEntity NewPercent(decimal value = 10m, int? maxUsage = null,
        DateTime? start = null, DateTime? end = null) =>
        DiscountEntity.Create("Spring Sale", DiscountType.Percentage, value, TenantId,
            startDate: start, endDate: end, maxUsageCount: maxUsage);

    // ── Creation ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Percent_ShouldInitialize()
    {
        var d = NewPercent();

        d.Name.Should().Be("Spring Sale");
        d.Type.Should().Be(DiscountType.Percentage);
        d.Value.Should().Be(10m);
        d.IsActive.Should().BeTrue();
        d.UsageCount.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldRaiseDiscountCreatedEvent()
    {
        var d = NewPercent();

        d.DomainEvents.Should().ContainSingle(e => e is DiscountCreatedEvent);
        var evt = (DiscountCreatedEvent)d.DomainEvents[0];
        evt.DiscountId.Should().Be(d.Id);
        evt.Type.Should().Be("Percentage");
    }

    [Fact]
    public void Create_BlankName_ShouldThrow()
    {
        var act = () => DiscountEntity.Create("", DiscountType.Percentage, 10m, TenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveValue_ShouldThrow(decimal value)
    {
        var act = () => DiscountEntity.Create("X", DiscountType.Percentage, value, TenantId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_PercentageOver100_ShouldThrow()
    {
        var act = () => DiscountEntity.Create("X", DiscountType.Percentage, 101m, TenantId);

        act.Should().Throw<ArgumentException>().WithMessage("*100*");
    }

    [Fact]
    public void Create_FixedAmountOver100_ShouldNotThrow()
    {
        var act = () => DiscountEntity.Create("X", DiscountType.FixedAmount, 500m, TenantId);

        act.Should().NotThrow();
    }

    // ── IsAvailable / IsExpired / IsStarted ─────────────────────────────────────

    [Fact]
    public void IsAvailable_NewDiscount_ShouldBeTrue()
    {
        NewPercent().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_PastEndDate_ShouldBeTrue()
    {
        var d = NewPercent(end: DateTime.UtcNow.AddDays(-1));

        d.IsExpired.Should().BeTrue();
        d.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsStarted_FutureStartDate_ShouldBeFalse()
    {
        var d = NewPercent(start: DateTime.UtcNow.AddDays(1));

        d.IsStarted.Should().BeFalse();
        d.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_AfterMaxUsage_ShouldBeFalse()
    {
        var d = NewPercent(maxUsage: 1);
        d.Apply(100m);

        d.IsAvailable.Should().BeFalse();
    }

    // ── Apply ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Percentage_ShouldDiscount()
    {
        var d = NewPercent(value: 25m);

        var result = d.Apply(100m);

        result.Should().Be(75m);
    }

    [Fact]
    public void Apply_FixedAmount_ShouldSubtract()
    {
        var d = DiscountEntity.Create("X", DiscountType.FixedAmount, 30m, TenantId);

        d.Apply(100m).Should().Be(70m);
    }

    [Fact]
    public void Apply_FixedAmountGreaterThanPrice_ShouldClampToZero()
    {
        var d = DiscountEntity.Create("X", DiscountType.FixedAmount, 200m, TenantId);

        d.Apply(50m).Should().Be(0m);
    }

    [Fact]
    public void Apply_ShouldIncrementUsageCount()
    {
        var d = NewPercent();

        d.Apply(100m);

        d.UsageCount.Should().Be(1);
    }

    [Fact]
    public void Apply_WhenUnavailable_ShouldThrow()
    {
        var d = NewPercent(end: DateTime.UtcNow.AddDays(-1));

        var act = () => d.Apply(100m);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Activate / Deactivate ───────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldFlipFlagAndRaiseEvent()
    {
        var d = NewPercent();
        d.ClearDomainEvents();

        d.Deactivate();

        d.IsActive.Should().BeFalse();
        d.DomainEvents.Should().ContainSingle(e => e is DiscountDeactivatedEvent);
    }

    [Fact]
    public void Activate_ShouldFlipFlag()
    {
        var d = NewPercent();
        d.Deactivate();

        d.Activate();

        d.IsActive.Should().BeTrue();
    }
}
