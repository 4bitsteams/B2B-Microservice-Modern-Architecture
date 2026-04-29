using B2B.Basket.Application.Commands.RemoveFromBasket;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Application;

public sealed class RemoveFromBasketHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly RemoveFromBasketHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public RemoveFromBasketHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _handler = new RemoveFromBasketHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_BasketMissing_ShouldReturnNotFound()
    {
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns((BasketEntity?)null);

        var result = await _handler.Handle(new RemoveFromBasketCommand(ProductId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Basket.NotFound");
    }

    [Fact]
    public async Task Handle_ItemMissing_ShouldReturnNotFound()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(new RemoveFromBasketCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Basket.ItemNotFound");
    }

    [Fact]
    public async Task Handle_Valid_ShouldRemoveItemAndPersist()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        basket.AddItem(ProductId, "Widget", "WGT-001", 10m, 1);
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(new RemoveFromBasketCommand(ProductId), default);

        result.IsSuccess.Should().BeTrue();
        basket.Items.Should().BeEmpty();
        await _repo.Received(1).SaveAsync(basket, Arg.Any<CancellationToken>());
    }
}
