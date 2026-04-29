using B2B.Discount.Application.Interfaces;
using B2B.Discount.Application.Queries.GetDiscountById;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Tests.Application;

public sealed class GetDiscountByIdHandlerTests
{
    private readonly IReadDiscountRepository _repo = Substitute.For<IReadDiscountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetDiscountByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetDiscountByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetDiscountByIdHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((DiscountEntity?)null);

        var result = await _handler.Handle(new GetDiscountByIdQuery(id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var d = DiscountEntity.Create("X", DiscountType.Percentage, 5m, Guid.NewGuid());
        _repo.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);

        var result = await _handler.Handle(new GetDiscountByIdQuery(d.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Found_ShouldMapToDto()
    {
        var d = DiscountEntity.Create("Spring", DiscountType.Percentage, 25m, TenantId);
        _repo.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);

        var result = await _handler.Handle(new GetDiscountByIdQuery(d.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(d.Id);
        result.Value.Name.Should().Be("Spring");
        result.Value.Type.Should().Be("Percentage");
        result.Value.Value.Should().Be(25m);
        result.Value.IsAvailable.Should().BeTrue();
    }
}
