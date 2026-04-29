using B2B.Discount.Application.Interfaces;
using B2B.Discount.Application.Queries.GetDiscounts;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Tests.Application;

public sealed class GetDiscountsHandlerTests
{
    private readonly IReadDiscountRepository _repo = Substitute.For<IReadDiscountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetDiscountsHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetDiscountsHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetDiscountsHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedDtos()
    {
        var d1 = DiscountEntity.Create("A", DiscountType.Percentage, 5m, TenantId);
        var d2 = DiscountEntity.Create("B", DiscountType.FixedAmount, 10m, TenantId);
        var paged = PagedList<DiscountEntity>.Create(new[] { d1, d2 }, 1, 20);
        _repo.GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _handler.Handle(new GetDiscountsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(x => x.Name).Should().Contain(new[] { "A", "B" });
    }

    [Fact]
    public async Task Handle_ShouldQueryCurrentTenant()
    {
        _repo.GetPagedByTenantAsync(TenantId, 2, 10, Arg.Any<CancellationToken>())
            .Returns(PagedList<DiscountEntity>.Create(Array.Empty<DiscountEntity>(), 2, 10));

        await _handler.Handle(new GetDiscountsQuery(2, 10), default);

        await _repo.Received(1).GetPagedByTenantAsync(TenantId, 2, 10, Arg.Any<CancellationToken>());
    }
}
