using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Commands.CancelOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Tests.Application;

public sealed class CancelOrderHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly CancelOrderHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test St", "Boston", "MA", "02101", "US");

    public CancelOrderHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);

        _handler = new CancelOrderHandler(_orderRepo, _unitOfWork, _currentUser);
    }

    private static OrderEntity MakeOrder(Guid? tenantId = null)
    {
        var order = OrderEntity.Create(
            Guid.NewGuid(), tenantId ?? TenantId, TestAddress, "ORD-CANCEL-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);
        return order;
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithPendingOrder_ShouldCancelSuccessfully()
    {
        var order = MakeOrder();
        var cmd = new CancelOrderCommand(order.Id, "Customer request");
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer request");
    }

    [Fact]
    public async Task Handle_WithConfirmedOrder_ShouldCancelSuccessfully()
    {
        var order = MakeOrder();
        order.Confirm();
        var cmd = new CancelOrderCommand(order.Id, "Order changed");
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WithValidCancel_ShouldPersistChanges()
    {
        var order = MakeOrder();
        var cmd = new CancelOrderCommand(order.Id, "Test");
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        await _handler.Handle(cmd, default);

        _orderRepo.Received(1).Update(order);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        _orderRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((OrderEntity?)null);

        var result = await _handler.Handle(
            new CancelOrderCommand(Guid.NewGuid(), "reason"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var order = MakeOrder(tenantId: Guid.NewGuid()); // different tenant
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(
            new CancelOrderCommand(order.Id, "reason"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyDelivered_ShouldReturnValidationError()
    {
        var order = MakeOrder();
        order.Confirm();
        order.StartProcessing();
        order.Ship("TRACK-XYZ");
        order.Deliver();

        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(
            new CancelOrderCommand(order.Id, "Too late"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Order.InvalidStatus");
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyCancelled_ShouldReturnValidationError()
    {
        var order = MakeOrder();
        order.Cancel("First cancellation");

        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(
            new CancelOrderCommand(order.Id, "Again"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
