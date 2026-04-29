using B2B.Basket.Application.Commands.UpdateBasketItem;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Application;

public sealed class UpdateBasketItemHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly UpdateBasketItemHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public UpdateBasketItemHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _handler = new UpdateBasketItemHandler(_repo, _currentUser);
    }

    private BasketEntity ExistingBasketWithItem(int qty)
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        basket.AddItem(ProductId, "Widget", "WGT-001", 10m, qty);
        return basket;
    }

    [Fact]
    public async Task Handle_BasketMissing_ShouldReturnNotFound()
    {
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns((BasketEntity?)null);

        var result = await _handler.Handle(new UpdateBasketItemCommand(ProductId, 5), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Basket.NotFound");
    }

    [Fact]
    public async Task Handle_ItemMissing_ShouldReturnNotFound()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(new UpdateBasketItemCommand(Guid.NewGuid(), 5), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Basket.ItemNotFound");
    }

    [Fact]
    public async Task Handle_ValidUpdate_ShouldChangeQuantityAndPersist()
    {
        var basket = ExistingBasketWithItem(qty: 1);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(new UpdateBasketItemCommand(ProductId, 4), default);

        result.IsSuccess.Should().BeTrue();
        basket.Items[0].Quantity.Should().Be(4);
        await _repo.Received(1).SaveAsync(basket, Arg.Any<CancellationToken>());
        result.Value.TotalItems.Should().Be(4);
        result.Value.TotalPrice.Should().Be(40m);
    }
}
