using B2B.Basket.Application.Commands.CheckoutBasket;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Messaging;
using FluentAssertions;
using NSubstitute;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Application;

public sealed class CheckoutBasketHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly CheckoutBasketHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly CheckoutBasketCommand ValidCommand = new(
        Street: "1 Main", City: "NYC", State: "NY", PostalCode: "10001", Country: "US");

    public CheckoutBasketHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _currentUser.Email.Returns("buyer@acme.test");
        _handler = new CheckoutBasketHandler(_repo, _currentUser, _eventBus);
    }

    [Fact]
    public async Task Handle_NoBasket_ShouldReturnValidationError()
    {
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns((BasketEntity?)null);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Basket.Empty");
    }

    [Fact]
    public async Task Handle_EmptyBasket_ShouldReturnValidationError()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Basket.Empty");
    }

    [Fact]
    public async Task Handle_ValidBasket_ShouldPublishIntegrationEvent()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 10m, 2);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        BasketCheckedOutIntegration? captured = null;
        await _eventBus.PublishAsync(
            Arg.Do<BasketCheckedOutIntegration>(e => captured = e),
            Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.CustomerId.Should().Be(UserId);
        captured.TenantId.Should().Be(TenantId);
        captured.CustomerEmail.Should().Be("buyer@acme.test");
        captured.TotalAmount.Should().Be(20m);
        captured.Items.Should().ContainSingle(i => i.ProductId == productId);
    }

    [Fact]
    public async Task Handle_ValidBasket_ShouldDeleteBasketAfterCheckout()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        basket.AddItem(Guid.NewGuid(), "Widget", "WGT-001", 10m, 1);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).DeleteAsync(UserId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidBasket_ShouldReturnTotals()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        basket.AddItem(Guid.NewGuid(), "A", "A", 5m, 3);
        basket.AddItem(Guid.NewGuid(), "B", "B", 7m, 2);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(15m + 14m);
        result.Value.ItemCount.Should().Be(5);
    }
}
