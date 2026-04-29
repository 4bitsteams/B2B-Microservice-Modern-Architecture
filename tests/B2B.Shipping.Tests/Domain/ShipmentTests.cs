using B2B.Shipping.Domain.Entities;
using B2B.Shipping.Domain.Events;
using FluentAssertions;
using Xunit;

namespace B2B.Shipping.Tests.Domain;

public sealed class ShipmentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Shipment New(decimal cost = 9.99m) =>
        Shipment.Create(OrderId, TenantId, "FedEx", "Alice", "1 Main", "NYC", "US", cost, "2-3 days");

    [Fact]
    public void Create_ShouldInitializePending()
    {
        var s = New();

        s.Status.Should().Be(ShipmentStatus.Pending);
        s.Carrier.Should().Be("FedEx");
        s.OrderId.Should().Be(OrderId);
        s.TrackingNumber.Should().StartWith("B2B-");
    }

    [Fact]
    public void Create_ShouldRaiseShipmentCreatedEvent()
    {
        var s = New();

        s.DomainEvents.Should().ContainSingle(e => e is ShipmentCreatedEvent);
    }

    [Fact]
    public void Create_BlankCarrier_ShouldThrow()
    {
        var act = () => Shipment.Create(OrderId, TenantId, "", "Alice", "1 Main", "NYC", "US", 5m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_BlankRecipient_ShouldThrow()
    {
        var act = () => Shipment.Create(OrderId, TenantId, "FedEx", "", "1 Main", "NYC", "US", 5m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeShippingCost_ShouldThrow()
    {
        var act = () => Shipment.Create(OrderId, TenantId, "FedEx", "Alice", "1 Main", "NYC", "US", -1m);

        act.Should().Throw<ArgumentException>();
    }

    // ── Ship ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ship_FromPending_ShouldTransitionAndStampShippedAt()
    {
        var s = New();
        s.ClearDomainEvents();

        s.Ship();

        s.Status.Should().Be(ShipmentStatus.Shipped);
        s.ShippedAt.Should().NotBeNull();
        s.DomainEvents.Should().ContainSingle(e => e is ShipmentDispatchedEvent);
    }

    [Fact]
    public void Ship_AfterDelivered_ShouldThrow()
    {
        var s = New();
        s.Ship();
        s.MarkDelivered();

        var act = () => s.Ship();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── UpdateTracking ─────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTracking_ShouldChangeNumber()
    {
        var s = New();

        s.UpdateTracking("ALT-001");

        s.TrackingNumber.Should().Be("ALT-001");
    }

    [Fact]
    public void UpdateTracking_Blank_ShouldThrow()
    {
        var s = New();

        var act = () => s.UpdateTracking("");

        act.Should().Throw<ArgumentException>();
    }

    // ── MarkDelivered ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkDelivered_FromShipped_ShouldTransitionAndRaiseEvent()
    {
        var s = New();
        s.Ship();
        s.ClearDomainEvents();

        s.MarkDelivered();

        s.Status.Should().Be(ShipmentStatus.Delivered);
        s.DeliveredAt.Should().NotBeNull();
        s.DomainEvents.Should().ContainSingle(e => e is ShipmentDeliveredEvent);
    }

    [Fact]
    public void MarkDelivered_FromPending_ShouldThrow()
    {
        var s = New();

        var act = () => s.MarkDelivered();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Cancel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromPending_ShouldTransition()
    {
        var s = New();

        s.Cancel();

        s.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AfterDelivered_ShouldThrow()
    {
        var s = New();
        s.Ship();
        s.MarkDelivered();

        var act = () => s.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }
}
