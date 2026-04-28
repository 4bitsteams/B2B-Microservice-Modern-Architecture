using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Commands.ConfirmOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Tests.Application;

public sealed class ConfirmOrderHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly ConfirmOrderHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test St", "Boston", "MA", "02101", "US");

    public ConfirmOrderHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ConfirmOrderHandler(_orderRepo, _unitOfWork, _currentUser);
    }

    private static OrderEntity MakeOrder(Guid? tenantId = null)
    {
        var order = OrderEntity.Create(
            Guid.NewGuid(), tenantId ?? TenantId, TestAddress, "ORD-CONFIRM-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);
        return order;
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithPendingOrder_ShouldReturnSuccess()
    {
        var order = MakeOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new ConfirmOrderCommand(order.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithPendingOrder_ShouldTransitionToConfirmed()
    {
        var order = MakeOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        await _handler.Handle(new ConfirmOrderCommand(order.Id), default);

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_WithPendingOrder_ShouldPersistChanges()
    {
        var order = MakeOrder();
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        await _handler.Handle(new ConfirmOrderCommand(order.Id), default);

        _orderRepo.Received(1).Update(order);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        _orderRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((OrderEntity?)null);

        var result = await _handler.Handle(new ConfirmOrderCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var order = MakeOrder(tenantId: Guid.NewGuid());
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new ConfirmOrderCommand(order.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyConfirmed_ShouldReturnValidation()
    {
        var order = MakeOrder();
        order.Confirm(); // already confirmed
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new ConfirmOrderCommand(order.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Order.InvalidStatus");
    }
}
