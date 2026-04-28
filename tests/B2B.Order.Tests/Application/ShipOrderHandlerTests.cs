using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Commands.ShipOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Tests.Application;

public sealed class ShipOrderHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly ShipOrderHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test St", "Boston", "MA", "02101", "US");

    public ShipOrderHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ShipOrderHandler(_orderRepo, _unitOfWork, _currentUser);
    }

    private static OrderEntity MakeProcessingOrder(Guid? tenantId = null)
    {
        var order = OrderEntity.Create(
            Guid.NewGuid(), tenantId ?? TenantId, TestAddress, "ORD-SHIP-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);
        order.Confirm();
        order.StartProcessing();
        return order;
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithProcessingOrder_ShouldReturnSuccess()
    {
        var order = MakeProcessingOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new ShipOrderCommand(order.Id, "TRACK-001"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithProcessingOrder_ShouldTransitionToShipped()
    {
        var order = MakeProcessingOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        await _handler.Handle(new ShipOrderCommand(order.Id, "TRACK-001"), default);

        order.Status.Should().Be(OrderStatus.Shipped);
        order.TrackingNumber.Should().Be("TRACK-001");
        order.ShippedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithProcessingOrder_ShouldPersistChanges()
    {
        var order = MakeProcessingOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        await _handler.Handle(new ShipOrderCommand(order.Id, "TRACK-001"), default);

        _orderRepo.Received(1).Update(order);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        _orderRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((OrderEntity?)null);

        var result = await _handler.Handle(new ShipOrderCommand(Guid.NewGuid(), "TRACK"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public async Task Handle_WhenOrderNotInProcessingStatus_ShouldReturnValidation()
    {
        // Pending order cannot be shipped directly
        var order = OrderEntity.Create(
            Guid.NewGuid(), TenantId, TestAddress, "ORD-SKIP-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);
        order.Confirm(); // Confirmed, not Processing
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new ShipOrderCommand(order.Id, "TRACK"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Order.InvalidStatus");
    }
}
