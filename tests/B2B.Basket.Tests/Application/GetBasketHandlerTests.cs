using B2B.Basket.Application.Interfaces;
using B2B.Basket.Application.Queries.GetBasket;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using BasketEntity = B2B.Basket.Domain.Entities.Basket;

namespace B2B.Basket.Tests.Application;

public sealed class GetBasketHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetBasketHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    public GetBasketHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetBasketHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NoBasket_ShouldReturnNotFound()
    {
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns((BasketEntity?)null);

        var result = await _handler.Handle(new GetBasketQuery(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Basket.NotFound");
    }

    [Fact]
    public async Task Handle_ExistingBasket_ShouldMapToDto()
    {
        var basket = BasketEntity.CreateFor(UserId, TenantId);
        var productId = Guid.NewGuid();
        basket.AddItem(productId, "Widget", "WGT-001", 5m, 4, "img.png");
        _repo.GetAsync(UserId, TenantId, Arg.Any<CancellationToken>()).Returns(basket);

        var result = await _handler.Handle(new GetBasketQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(UserId);
        result.Value.TotalItems.Should().Be(4);
        result.Value.TotalPrice.Should().Be(20m);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductId.Should().Be(productId);
        result.Value.Items[0].ImageUrl.Should().Be("img.png");
        result.Value.Items[0].TotalPrice.Should().Be(20m);
    }
}
