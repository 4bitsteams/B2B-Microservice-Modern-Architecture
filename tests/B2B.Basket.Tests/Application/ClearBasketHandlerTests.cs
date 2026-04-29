using B2B.Basket.Application.Commands.ClearBasket;
using B2B.Basket.Application.Interfaces;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Basket.Tests.Application;

public sealed class ClearBasketHandlerTests
{
    private readonly IBasketRepository _repo = Substitute.For<IBasketRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    public ClearBasketHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
    }

    [Fact]
    public async Task Handle_ShouldDeleteBasketAndReturnSuccess()
    {
        var handler = new ClearBasketHandler(_repo, _currentUser);

        var result = await handler.Handle(new ClearBasketCommand(), default);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(UserId, TenantId, Arg.Any<CancellationToken>());
    }
}
