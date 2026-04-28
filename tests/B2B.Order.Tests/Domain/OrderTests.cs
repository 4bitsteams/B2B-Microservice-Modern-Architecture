using FluentAssertions;
using Xunit;
using B2B.Order.Domain.ValueObjects;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;
using OrderConfirmedEvent = B2B.Order.Domain.Events.OrderConfirmedEvent;
using OrderCancelledEvent = B2B.Order.Domain.Events.OrderCancelledEvent;

namespace B2B.Order.Tests.Domain;

public sealed class OrderTests
{
    private static readonly Address TestAddress = Address.Create(
        "123 Main St", "New York", "NY", "10001", "US");

    private static OrderEntity CreateOrder()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TestAddress, "ORD-001");
        order.AddItem(Guid.NewGuid(), "Product A", "SKU-001", 50m, 2);
        return order;
    }

    [Fact]
    public void Create_ShouldGenerateOrderNumber()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TestAddress, "ORD-TEST-001");
        order.OrderNumber.Should().StartWith("ORD-");
    }

    [Fact]
    public void Confirm_ShouldRaiseOrderConfirmedEvent()
    {
        var order = CreateOrder();
        order.ClearDomainEvents();

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderConfirmedEvent);
    }

    [Fact]
    public void Cancel_FromPendingStatus_ShouldSucceed()
    {
        var order = CreateOrder();
        order.ClearDomainEvents();

        order.Cancel("Customer request");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer request");
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_AfterDelivered_ShouldThrow()
    {
        var order = CreateOrder();
        order.Confirm();
        order.StartProcessing();
        order.Ship("TRACK-001");
        order.Deliver();

        var act = () => order.Cancel("Too late");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddItem_WithExistingProduct_ShouldIncrementQuantity()
    {
        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TestAddress, "ORD-002");
        order.AddItem(productId, "Product A", "SKU-A", 25m, 2);
        order.AddItem(productId, "Product A", "SKU-A", 25m, 3);

        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public void TotalAmount_ShouldIncludeTaxAndShipping()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TestAddress, "ORD-003");
        order.AddItem(Guid.NewGuid(), "Product A", "SKU-A", 100m, 1);
        order.ApplyTaxRate(0.10m);
        order.ApplyShippingCost(10m);

        order.Subtotal.Should().Be(100m);
        order.TaxAmount.Should().Be(10m);
        order.TotalAmount.Should().Be(120m);
    }

    [Fact]
    public void Ship_WithoutConfirming_ShouldThrow()
    {
        var order = CreateOrder();
        var act = () => order.Ship("TRACK-001");
        act.Should().Throw<InvalidOperationException>();
    }
}
