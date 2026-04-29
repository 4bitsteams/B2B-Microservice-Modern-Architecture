using B2B.Discount.Application.Commands.CreateCoupon;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Discount.Tests.Application;

public sealed class CreateCouponHandlerTests
{
    private readonly ICouponRepository _repo = Substitute.For<ICouponRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateCouponHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly CreateCouponCommand ValidCommand = new(
        Code: "SAVE10",
        Name: "Save 10%",
        Type: DiscountType.Percentage,
        Value: 10m,
        MaxUsageCount: 5);

    public CreateCouponHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByCodeAsync("SAVE10", TenantId, Arg.Any<CancellationToken>()).Returns((Coupon?)null);
        _handler = new CreateCouponHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("SAVE10");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<Coupon>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCode_ShouldReturnConflict()
    {
        var existing = Coupon.Create("SAVE10", "Old", DiscountType.Percentage, 5m, TenantId);
        _repo.GetByCodeAsync("SAVE10", TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Coupon.CodeExists");
    }
}
