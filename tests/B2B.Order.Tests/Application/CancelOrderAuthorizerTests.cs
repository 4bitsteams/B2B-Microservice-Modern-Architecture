using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Commands.CancelOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Tests.Application;

public sealed class CancelOrderAuthorizerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly CancelOrderAuthorizer _authorizer;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static readonly Address TestAddress = Address.Create(
        "1 Test Ave", "Seattle", "WA", "98101", "US");

    public CancelOrderAuthorizerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _currentUser.UserId.Returns(OwnerId);

        _authorizer = new CancelOrderAuthorizer(_orderRepo, _currentUser);
    }

    private OrderEntity MakeOrder(Guid? customerId = null, Guid? tenantId = null)
    {
        var order = OrderEntity.Create(
            customerId ?? OwnerId,
            tenantId ?? TenantId,
            TestAddress,
            "ORD-AUTH-001");
        order.AddItem(Guid.NewGuid(), "Widget", "WGT-1", 10m, 1);
        return order;
    }

    // ── Pass-through cases (let handler produce the right error) ────────────────

    [Fact]
    public async Task AuthorizeAsync_WhenOrderNotFound_ShouldSucceed()
    {
        _orderRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((OrderEntity?)null);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(Guid.NewGuid(), "reason"));

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenOrderBelongsToDifferentTenant_ShouldSucceed()
    {
        var foreignOrder = MakeOrder(tenantId: Guid.NewGuid());
        _orderRepo.GetByIdAsync(foreignOrder.Id, default).Returns(foreignOrder);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(foreignOrder.Id, "reason"));

        // Pass through — handler will return NotFound (no existence leak)
        result.IsAuthorized.Should().BeTrue();
    }

    // ── Allowed roles ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_WhenUserIsTenantAdmin_ShouldSucceed()
    {
        var order = MakeOrder(customerId: Guid.NewGuid()); // different owner
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);
        _currentUser.IsInRole("TenantAdmin").Returns(true);
        _currentUser.IsInRole("SuperAdmin").Returns(false);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(order.Id, "admin cancel"));

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenUserIsSuperAdmin_ShouldSucceed()
    {
        var order = MakeOrder(customerId: Guid.NewGuid()); // different owner
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);
        _currentUser.IsInRole("TenantAdmin").Returns(false);
        _currentUser.IsInRole("SuperAdmin").Returns(true);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(order.Id, "super cancel"));

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenUserIsOrderOwner_ShouldSucceed()
    {
        var order = MakeOrder(customerId: OwnerId); // same owner as currentUser
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);
        _currentUser.IsInRole("TenantAdmin").Returns(false);
        _currentUser.IsInRole("SuperAdmin").Returns(false);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(order.Id, "my cancel"));

        result.IsAuthorized.Should().BeTrue();
    }

    // ── Denied ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_WhenNonAdminNonOwner_ShouldFail()
    {
        var order = MakeOrder(customerId: Guid.NewGuid()); // different owner
        _orderRepo.GetByIdAsync(order.Id, default).Returns(order);
        _currentUser.IsInRole("TenantAdmin").Returns(false);
        _currentUser.IsInRole("SuperAdmin").Returns(false);

        var result = await _authorizer.AuthorizeAsync(
            new CancelOrderCommand(order.Id, "sneaky cancel"));

        result.IsAuthorized.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }
}
