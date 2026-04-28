using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Interfaces;
using B2B.Order.Application.Queries.GetOrders;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Tests.Application;

public sealed class GetOrdersHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadOrderRepository _orderRepo = Substitute.For<IReadOrderRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetOrdersHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test St", "Boston", "MA", "02101", "US");

    public GetOrdersHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _currentUser.UserId.Returns(CustomerId);
        _currentUser.IsInRole("TenantAdmin").Returns(false);
        _currentUser.IsInRole("SuperAdmin").Returns(false);

        _handler = new GetOrdersHandler(_orderRepo, _currentUser);
    }

    private static OrderEntity MakeOrder(Guid? customerId = null)
    {
        var order = OrderEntity.Create(
            customerId ?? CustomerId, TenantId, TestAddress, "ORD-LIST-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 50m, 2);
        return order;
    }

    private static PagedList<OrderEntity> MakePage(IEnumerable<OrderEntity> orders) =>
        PagedList<OrderEntity>.Create(orders, 1, 20);

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AsRegularCustomer_ShouldReturnOwnOrdersOnly()
    {
        var page = MakePage(new[] { MakeOrder() });
        _orderRepo.GetPagedByCustomerAsync(CustomerId, TenantId, 1, 20, default)
            .Returns(page);

        var result = await _handler.Handle(new GetOrdersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        await _orderRepo.Received(1).GetPagedByCustomerAsync(
            CustomerId, TenantId, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AsTenantAdmin_ShouldReturnAllTenantOrders()
    {
        _currentUser.IsInRole("TenantAdmin").Returns(true);
        var page = MakePage(new[] { MakeOrder(Guid.NewGuid()), MakeOrder(Guid.NewGuid()) });
        _orderRepo.GetPagedByTenantAsync(TenantId, 1, 20, null, default)
            .Returns(page);

        var result = await _handler.Handle(new GetOrdersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        await _orderRepo.Received(1).GetPagedByTenantAsync(
            TenantId, 1, 20, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMapOrdersToSummaryDtos()
    {
        var order = MakeOrder();
        var page = MakePage(new[] { order });
        _orderRepo.GetPagedByCustomerAsync(CustomerId, TenantId, 1, 20, default)
            .Returns(page);

        var result = await _handler.Handle(new GetOrdersQuery(1, 20), default);

        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].OrderNumber.Should().Be("ORD-LIST-001");
        result.Value.Items[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_AsTenantAdmin_WithStatusFilter_ShouldPassStatusToRepository()
    {
        _currentUser.IsInRole("TenantAdmin").Returns(true);
        _orderRepo.GetPagedByTenantAsync(TenantId, 1, 20, OrderStatus.Confirmed, default)
            .Returns(MakePage(Array.Empty<OrderEntity>()));

        await _handler.Handle(new GetOrdersQuery(1, 20, OrderStatus.Confirmed), default);

        await _orderRepo.Received(1).GetPagedByTenantAsync(
            TenantId, 1, 20, OrderStatus.Confirmed, Arg.Any<CancellationToken>());
    }
}
