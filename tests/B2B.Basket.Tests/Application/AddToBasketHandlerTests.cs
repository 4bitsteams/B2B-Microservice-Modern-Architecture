using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Application;

public sealed class AddToBasketHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly AddToBasketHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly AddToBasketCommand ValidCommand = new(
        ProductId: Guid.NewGuid(),
        ProductName: "Widget Pro",
        Sku: "WGT-001",
        UnitPrice: 25m,
        Quantity: 2);

    public AddToBasketHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetOrCreateAsync(UserId, TenantId, Arg.Any<CancellationToken>())
            .Returns(_ => BasketEntity.CreateFor(UserId, TenantId));

        _handler = new AddToBasketHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(UserId);
        result.Value.TotalItems.Should().Be(2);
        result.Value.TotalPrice.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_ShouldPersistBasket()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).SaveAsync(Arg.Any<BasketEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldGetOrCreateForCurrentUser()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).GetOrCreateAsync(UserId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AddSameProductTwice_ShouldAccumulateQuantity()
    {
        BasketEntity? saved = null;
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        _repo.GetOrCreateAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);
        _repo.SaveAsync(Arg.Do<BasketEntity>(b => saved = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(ValidCommand, default);
        await _handler.Handle(ValidCommand, default);

        saved!.Items.Should().ContainSingle();
        saved.Items[0].Quantity.Should().Be(4);
    }
}
