using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Order.Application.Commands.CreateOrder;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Tests.Application;

public sealed class CreateOrderHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private readonly IOrderNumberGenerator _orderNumberGen = Substitute.For<IOrderNumberGenerator>();

    private readonly CreateOrderHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly AddressDto ValidAddress = new(
        "123 Main St", "New York", "NY", "10001", "US");

    private static readonly IReadOnlyList<OrderItemRequest> OneItem =
    [
        new(Guid.NewGuid(), "Widget Pro", "WGT-001", 100m, 2)
    ];

    private static readonly CreateOrderCommand ValidCommand = new(
        ValidAddress, BillingAddress: null, OneItem, Notes: null)
    {
        IdempotencyKey = "key-001"
    };

    public CreateOrderHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _orderNumberGen.Generate().Returns("ORD-TEST-001");
        _taxService.GetTaxRateAsync(
            Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        _handler = new CreateOrderHandler(
            _orderRepo, _currentUser, _unitOfWork, _taxService, _orderNumberGen);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnCorrectOrderNumber()
    {
        _orderNumberGen.Generate().Returns("ORD-CUSTOM-999");

        var result = await _handler.Handle(ValidCommand, default);

        result.Value!.OrderNumber.Should().Be("ORD-CUSTOM-999");
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldConfirmOrderImmediately()
    {
        OrderEntity? captured = null;
        await _orderRepo.AddAsync(
            Arg.Do<OrderEntity>(o => captured = o),
            Arg.Any<CancellationToken>());

        await _handler.Handle(ValidCommand, default);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldApplyTaxRate()
    {
        _taxService.GetTaxRateAsync(
            Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(0.10m);

        OrderEntity? captured = null;
        await _orderRepo.AddAsync(
            Arg.Do<OrderEntity>(o => captured = o),
            Arg.Any<CancellationToken>());

        await _handler.Handle(ValidCommand, default);

        captured.Should().NotBeNull();
        captured!.TaxRate.Should().Be(0.10m);
        captured.TaxAmount.Should().BeApproximately(20m, precision: 0.01m); // 100*2 * 10%
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistOrder()
    {
        await _handler.Handle(ValidCommand, default);

        await _orderRepo.Received(1).AddAsync(
            Arg.Any<OrderEntity>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithBillingAddress_ShouldPopulateBillingAddress()
    {
        var billingAddress = new AddressDto("99 Bill St", "Chicago", "IL", "60601", "US");
        var cmd = ValidCommand with { BillingAddress = billingAddress };

        OrderEntity? captured = null;
        await _orderRepo.AddAsync(
            Arg.Do<OrderEntity>(o => captured = o),
            Arg.Any<CancellationToken>());

        await _handler.Handle(cmd, default);

        captured!.BillingAddress.Should().NotBeNull();
        captured.BillingAddress!.City.Should().Be("Chicago");
    }

    [Fact]
    public async Task Handle_WithMultipleItems_ShouldComputeSubtotalCorrectly()
    {
        var twoItems = new List<OrderItemRequest>
        {
            new(Guid.NewGuid(), "Widget A", "WGT-A", 50m, 3),  // 150
            new(Guid.NewGuid(), "Widget B", "WGT-B", 25m, 2)   //  50
        };
        var cmd = ValidCommand with { Items = twoItems };

        OrderEntity? captured = null;
        await _orderRepo.AddAsync(
            Arg.Do<OrderEntity>(o => captured = o),
            Arg.Any<CancellationToken>());

        await _handler.Handle(cmd, default);

        captured!.Subtotal.Should().Be(200m);
    }
}
