using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Interfaces;
using B2B.Order.Application.Queries.GetOrderById;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Tests.Application;

public sealed class GetOrderByIdHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadOrderRepository _orderRepo = Substitute.For<IReadOrderRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetOrderByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test St", "Boston", "MA", "02101", "US");

    public GetOrderByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _currentUser.UserId.Returns(CustomerId);
        _currentUser.IsInRole("TenantAdmin").Returns(false);
        _currentUser.IsInRole("SuperAdmin").Returns(false);

        _handler = new GetOrderByIdHandler(_orderRepo, _currentUser);
    }

    private static OrderEntity MakeOrder(Guid? customerId = null, Guid? tenantId = null)
    {
        var order = OrderEntity.Create(
            customerId ?? CustomerId, tenantId ?? TenantId, TestAddress, "ORD-DETAIL-001");
        order.AddItem(Guid.NewGuid(), "Widget Pro", "WGT-PRO", 49.99m, 2);
        return order;
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AsOwner_ShouldReturnOrderDetail()
    {
        var order = MakeOrder();
        _orderRepo.GetWithItemsAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderNumber.Should().Be("ORD-DETAIL-001");
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductName.Should().Be("Widget Pro");
    }

    [Fact]
    public async Task Handle_AsTenantAdmin_CanViewAnyOrderInTenant()
    {
        _currentUser.IsInRole("TenantAdmin").Returns(true);
        var order = MakeOrder(customerId: Guid.NewGuid()); // different owner
        _orderRepo.GetWithItemsAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        _orderRepo.GetWithItemsAsync(Arg.Any<Guid>(), default).Returns((OrderEntity?)null);

        var result = await _handler.Handle(new GetOrderByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToDifferentTenant_ShouldReturnNotFound()
    {
        var order = MakeOrder(tenantId: Guid.NewGuid());
        _orderRepo.GetWithItemsAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AsNonAdminNonOwner_ShouldReturnForbidden()
    {
        var order = MakeOrder(customerId: Guid.NewGuid()); // different owner
        _orderRepo.GetWithItemsAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Order.Forbidden");
    }

    [Fact]
    public async Task Handle_ShouldMapShippingAddressToDto()
    {
        var order = MakeOrder();
        _orderRepo.GetWithItemsAsync(order.Id, default).Returns(order);

        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), default);

        result.Value!.ShippingAddress.City.Should().Be("Boston");
        result.Value.ShippingAddress.Country.Should().Be("US");
    }
}
