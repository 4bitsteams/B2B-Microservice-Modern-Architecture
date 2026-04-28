using FluentAssertions;
using Xunit;
using B2B.Order.Domain.Entities;

namespace B2B.Order.Tests.Domain;

public sealed class OrderItemTests
{
    // ── Create ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = OrderItem.Create(orderId, productId, "Widget Pro", "WGT-001", 49.99m, 3);

        item.OrderId.Should().Be(orderId);
        item.ProductId.Should().Be(productId);
        item.ProductName.Should().Be("Widget Pro");
        item.Sku.Should().Be("WGT-001");
        item.UnitPrice.Should().Be(49.99m);
        item.Quantity.Should().Be(3);
        item.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void TotalPrice_ShouldBeUnitPriceTimesQuantity()
    {
        var item = OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Widget", "WGT-1", 25m, 4);

        item.TotalPrice.Should().Be(100m);
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldThrow()
    {
        var act = () => OrderItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Widget", "WGT-1", 10m, 0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Quantity*");
    }

    [Fact]
    public void Create_WithNegativeUnitPrice_ShouldThrow()
    {
        var act = () => OrderItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Widget", "WGT-1", -5m, 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*price*");
    }

    // ── IncrementQuantity ───────────────────────────────────────────────────────

    [Fact]
    public void IncrementQuantity_ShouldAddToExistingQuantity()
    {
        var item = OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Widget", "WGT-1", 10m, 2);

        item.IncrementQuantity(3);

        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void IncrementQuantity_WithZeroAmount_ShouldThrow()
    {
        var item = OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);

        var act = () => item.IncrementQuantity(0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Amount*");
    }
}
